using System.Dynamic;
using System.Net.Mime;
using Asp.Versioning;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Entries;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api.Services.Sorting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Controllers;

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
            .Where(e => e.HabitId == null || e.HabitId == query.HabitId)
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
        EntryQueryParameters query,
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
            .Where(e => e.UserId == userId)
            .Where(e => e.Id == id)
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
        ];

        return links;
    }
}
