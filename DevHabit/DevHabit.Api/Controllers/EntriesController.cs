using System.Dynamic;
using System.Net.Mime;
using Asp.Versioning;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Entries;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api.Services.Sorting;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Controllers;

[EnableRateLimiting("default")]
[Authorize(Roles = Roles.Member)]
[ApiController]
[Route("entries")]
[ApiVersion(1.0)]
[Produces(
    MediaTypeNames.Application.Json,
    CustomMediaTypeNames.Application.JsonV1,
    CustomMediaTypeNames.Application.HateoasJson,
    CustomMediaTypeNames.Application.HateoasJsonV1)]
public sealed class EntriesController(
    ApplicationDbContext dbContext,
    UserContext userContext,
    LinkService linkService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginationResult<ExpandoObject>>> GetEntries(
        [FromQuery] EntriesQueryParameters query,
        SortMappingProvider sortMappingProvider,
        DataShapingService dataShapingService)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (!sortMappingProvider.ValidateMappings<EntryDto, Entry>(query.Sort))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided sort parameter isn't valid: '{query.Sort}'");
        }

        if (!dataShapingService.Validate<Entry>(query.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided fields parameter isn't valid: '{query.Fields}'");
        }

        SortMapping[] sortMappings = sortMappingProvider.GetMappings<EntryDto, Entry>();

        IQueryable<Entry> entriesQuery = dbContext.Entries
            .Where(e => e.UserId == userId)
            .Where(e => query.HabitId == null || e.HabitId == query.HabitId)
            .Where(e => query.FromDate == null || e.Date >= query.FromDate)
            .Where(e => query.ToDate == null || e.Date <= query.ToDate)
            .Where(e => query.Source == null || e.Source == query.Source)
            .Where(e => query.IsArchived == null || e.IsArchived == query.IsArchived);

        int count = await entriesQuery.CountAsync();

        List<EntryDto> entries = await entriesQuery
            .ApplySort(query.Sort, sortMappings)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(EntryQueries.ProjectToDto())
            .ToListAsync();

        var paginationResult = new PaginationResult<ExpandoObject>
        {
            Items = dataShapingService.ShapeCollectionData(
                    entries, 
                    query.Fields,
                    query.IncludLinks ? 
                        e => CreateLinksForEntry(e.Id, query.Fields, e.IsArchived) : 
                        null),
            TotalCount = count,
            Page = query.Page,
            PageSize = query.PageSize
        };

        if (query.IncludLinks)
        {
            paginationResult.Links = CreateLinksForEntries(
                query,
                paginationResult.HasPreviousPage,
                paginationResult.HasNextPage);
        }

        return Ok(paginationResult);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetEntry(
        string id,
        [FromQuery] EntryQueryParameters query,
        DataShapingService dataShapingService)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (!dataShapingService.Validate<Entry>(query.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided fields parameter isn't valid: '{query.Fields}'");
        }

        EntryDto? entryDto = await dbContext.Entries
            .Where(e => e.Id == id && e.UserId == userId)
            .Select(EntryQueries.ProjectToDto())
            .FirstOrDefaultAsync();
        
        if (entryDto == null)
        {
            return NotFound();
        }

        ExpandoObject expandoObject = dataShapingService.ShapeData(entryDto, query.Fields);

        if (query.IncludLinks)
        {
            ((IDictionary<string, object?>)expandoObject)[nameof(ILinksResponse.Links)] = 
                CreateLinksForEntry(id, query.Fields, entryDto.IsArchived);
        }

        return Ok(expandoObject);
    }

    [HttpPost]
    public async Task<ActionResult<EntryDto>> CreateEntry(
        CreateEntryDto createEntryDto,
        [FromHeader] AcceptHeaderDto acceptHeader,
        IValidator<CreateEntryDto> validator)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        await validator.ValidateAndThrowAsync(createEntryDto);

        Habit? habit = await dbContext.Habits
            .Where(h => h.UserId == userId && h.Id == createEntryDto.HabitId)
            .FirstOrDefaultAsync();

        if (habit is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"Habit with ID '{createEntryDto.HabitId}' does not exist.");
        }

        Entry entry = createEntryDto.ToEntity(userId, habit);

        dbContext.Entries.Add(entry);

        await dbContext.SaveChangesAsync();

        EntryDto entryDto = entry.ToDto();

        if (acceptHeader.IncludLinks)
        {
            entryDto.Links = CreateLinksForEntry(entry.Id, null, entry.IsArchived);
        }

        return CreatedAtAction(
            nameof(GetEntry),
            new { id = entry.Id },
            entryDto);
    }

    [HttpPost("batch")]
    public async Task<ActionResult<List<EntryDto>>> CreateEntryBatch(
        CreateEntryBatchDto createEntryBatchDto,
        [FromHeader] AcceptHeaderDto acceptHeader,
        IValidator<CreateEntryBatchDto> validator)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        await validator.ValidateAndThrowAsync(createEntryBatchDto);

        var habitIds = createEntryBatchDto.Entries
            .Select(e => e.HabitId)
            .ToHashSet();

        List<Habit> habits = await dbContext.Habits
            .Where(h => habitIds.Contains(h.Id) && h.UserId == userId)
            .ToListAsync();

        if (habits.Count != habitIds.Count)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "One or more habits is invalid.");
        }

        var entries = createEntryBatchDto.Entries
            .Select(dto => dto.ToEntity(userId, habits.First(h => h.Id == dto.HabitId)))
            .ToList();

        dbContext.Entries.AddRange(entries);

        await dbContext.SaveChangesAsync();

        var entryDtos = entries
            .Select(EntryMappings.ToDto)
            .ToList();
        
        if (acceptHeader.IncludLinks)
        {
            foreach (EntryDto entryDto in entryDtos)
            {
                entryDto.Links = CreateLinksForEntry(entryDto.Id, null, entryDto.IsArchived);
            }
        }

        return CreatedAtAction(nameof(GetEntries), entryDtos);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEntry(
        string id,
        UpdateEntryDto updateEntryDto,
        IValidator<UpdateEntryDto> validator)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        await validator.ValidateAndThrowAsync(updateEntryDto);

        Entry? entry = await dbContext.Entries
            .Where(e => e.UserId == userId && e.Id == id)
            .FirstOrDefaultAsync();

        if (entry is null)
        {
            return NotFound();
        }

        entry.UpdateFromDto(updateEntryDto);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEntry(string id)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        Entry? entry = await dbContext.Entries
            .Where(e => e.UserId == userId && e.Id == id)
            .FirstOrDefaultAsync();

        if (entry is null)
        {
            return NotFound();
        }

        dbContext.Entries.Remove(entry);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id}/archive")]
    public async Task<IActionResult> ArchiveEntry(string id)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        Entry? entry = await dbContext.Entries
            .Where(e => e.UserId == userId && e.Id == id)
            .FirstOrDefaultAsync();

        if (entry is null)
        {
            return NotFound();
        }

        entry.IsArchived = true;
        entry.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id}/unarchive")]
    public async Task<IActionResult> UnArchiveEntry(string id)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        Entry? entry = await dbContext.Entries
            .Where(e => e.UserId == userId && e.Id == id)
            .FirstOrDefaultAsync();

        if (entry is null)
        {
            return NotFound();
        }

        entry.IsArchived = false;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private List<LinkDto> CreateLinksForEntries(EntriesQueryParameters query, bool hasPreviousPage, bool hasNextPage)
    {
        List<LinkDto> links = 
        [
            linkService.Create(nameof(GetEntries), "self", HttpMethods.Get, new 
            {
                page = query.Page,
                pageSize = query.PageSize,
                sort = query.Sort,
                fields = query.Fields,
                habitId = query.HabitId,
                fromDate = query.FromDate,
                toDate = query.ToDate,
                source = query.Source,
                isArchived = query.IsArchived,
            }),
            linkService.Create(nameof(CreateEntry), "create", HttpMethods.Post),
            linkService.Create(nameof(CreateEntryBatch), "create-batch", HttpMethods.Post)
        ];

        if (hasPreviousPage)
        {
            links.Add(linkService.Create(nameof(GetEntries), "previous-page", HttpMethods.Get, new 
            {
                page = query.Page - 1,
                pageSize = query.PageSize,
                sort = query.Sort,
                fields = query.Fields,
                habitId = query.HabitId,
                fromDate = query.FromDate,
                toDate = query.ToDate,
                source = query.Source,
                isArchived = query.IsArchived,
            }));
        }

        if (hasNextPage)
        {
            links.Add(linkService.Create(nameof(GetEntries), "next-page", HttpMethods.Get, new 
            {
                page = query.Page + 1,
                pageSize = query.PageSize,
                sort = query.Sort,
                fields = query.Fields,
                habitId = query.HabitId,
                fromDate = query.FromDate,
                toDate = query.ToDate,
                source = query.Source,
                isArchived = query.IsArchived,
            }));
        }

        return links;
    }

    private List<LinkDto> CreateLinksForEntry(string id, string? fields, bool isArchived)
    {
        List<LinkDto> links = 
        [
            linkService.Create(nameof(GetEntries), "self", HttpMethods.Get, new { id, fields }),
            linkService.Create(nameof(UpdateEntry), "update", HttpMethods.Put, new { id }),
            linkService.Create(nameof(DeleteEntry), "delete", HttpMethods.Put, new { id }),
            isArchived ?
                linkService.Create(nameof(UnArchiveEntry), "un-archive", HttpMethods.Put, new { id }) :
                linkService.Create(nameof(ArchiveEntry), "archive", HttpMethods.Put, new { id }),
        ];

        return links;
    }
}
