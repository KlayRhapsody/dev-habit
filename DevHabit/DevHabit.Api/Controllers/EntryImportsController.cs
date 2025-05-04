
using System.Net.Mime;
using Asp.Versioning;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.EntryImports;
using DevHabit.Api.Entities;
using DevHabit.Api.Jobs;
using DevHabit.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace DevHabit.Api.Controllers;

[Authorize(Roles = Roles.Member)]
[ApiController]
[Route("entries/imports")]
[ApiVersion("1.0")]
[Produces(
    MediaTypeNames.Application.Json,
    CustomMediaTypeNames.Application.JsonV1,
    CustomMediaTypeNames.Application.HateoasJson,
    CustomMediaTypeNames.Application.HateoasJsonV1
)]
public sealed class EntryImportsController(
    ApplicationDbContext dbContext,
    ISchedulerFactory schedulerFactory,
    LinkService linkService,
    UserContext userContext
) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EntryImportJobDto>> CreateImportJob(
        [FromForm] CreateEntryImportJobDto createImportJobDto,
        [FromHeader] AcceptHeaderDto acceptHeaderDto,
        IValidator<CreateEntryImportJobDto> validator
    )
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        await validator.ValidateAndThrowAsync(createImportJobDto);

        using var memoryStream = new MemoryStream();
        await createImportJobDto.File.CopyToAsync(memoryStream);

        var importJob = new EntryImportJob
        {
            Id = EntryImportJob.NewId(),
            UserId = userId,
            Status = EntryImportStatus.Pending,
            FileName = createImportJobDto.File.FileName,
            FileContent = memoryStream.ToArray(),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.EntryImportJobs.Add(importJob);
        await dbContext.SaveChangesAsync();

        IScheduler scheduler = await schedulerFactory.GetScheduler();

        IJobDetail jobDetail = JobBuilder.Create<ProcessEntryImportJob>()
            .WithIdentity($"process-entry-import-{importJob.Id}")
            .UsingJobData("importJobId", importJob.Id)
            .Build();

        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity($"process-entry-import-trigger-{importJob.Id}")
            .StartNow()
            .Build();

        await scheduler.ScheduleJob(jobDetail, trigger);

        EntryImportJobDto importJobDto = importJob.ToDto();

        if (acceptHeaderDto.IncludLinks)
        {
            importJobDto.Links = CreateLinksForImportJob(importJobDto.Id);
        }

        return CreatedAtAction(nameof(GetImportJob), new { id = importJobDto.Id }, importJobDto);
    }

    [HttpGet]
    public async Task<ActionResult<PaginationResult<EntryImportJobDto>>> GetImportJobs(
        [FromHeader] AcceptHeaderDto acceptHeaderDto,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        IQueryable<EntryImportJob> query = dbContext.EntryImportJobs
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAtUtc);

        int totalCount = await query.CountAsync();

        List<EntryImportJobDto> importJobDtos = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(EntryImportQueries.ProjectToDto())
            .ToListAsync();

        if (acceptHeaderDto.IncludLinks)
        {
            foreach (EntryImportJobDto importJobDto in importJobDtos)
            {
                importJobDto.Links = CreateLinksForImportJob(importJobDto.Id);
            }
        }

        var result = new PaginationResult<EntryImportJobDto>
        {
            Items = importJobDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        if (acceptHeaderDto.IncludLinks)
        {
            result.Links = CreateLinksForImportJobs(page, pageSize, result.HasNextPage, result.HasPreviousPage);
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EntryImportJobDto>> GetImportJob(
        string id,
        [FromHeader] AcceptHeaderDto acceptHeaderDto)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        EntryImportJobDto? importJob = await dbContext.EntryImportJobs
            .Where(e => e.Id == id && e.UserId == userId)
            .Select(EntryImportQueries.ProjectToDto())
            .FirstOrDefaultAsync();

        if (importJob is null)
        {
            return NotFound();
        }

        if (acceptHeaderDto.IncludLinks)
        {
            importJob.Links = CreateLinksForImportJob(importJob.Id);
        }

        return Ok(importJob);
    }

    private List<LinkDto> CreateLinksForImportJobs(int page, int pageSize, bool hasNextPage, bool hasPreviousPage)
    {
        List<LinkDto> links = new()
        {
            linkService.Create(nameof(GetImportJobs), "self", HttpMethods.Get, new { page, pageSize })
        };

        if (hasNextPage)
        {
            links.Add(linkService.Create(nameof(GetImportJobs), "next-page", HttpMethods.Get, new { page = page + 1, pageSize }));
        }

        if (hasPreviousPage)
        {
            links.Add(linkService.Create(nameof(GetImportJobs), "previous-page", HttpMethods.Get, new { page = page - 1, pageSize }));
        }
        
        return links;
    }

    private List<LinkDto>? CreateLinksForImportJob(string id)
    {
        return
        [
            linkService.Create(nameof(GetImportJob), "self", HttpMethods.Get, new { id }),
        ];
    }
}
