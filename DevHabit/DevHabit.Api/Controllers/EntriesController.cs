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

    [HttpGet("cursor")]
    public async Task<IActionResult> GetEntriesCursor(
        [FromQuery] EntriesCursorQueryParameters query,
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

        IQueryable<Entry> entriesQuery = dbContext.Entries
            .Where(e => e.UserId == userId)
            .Where(e => query.HabitId == null || e.HabitId == query.HabitId)
            .Where(e => query.FromDate == null || e.Date >= query.FromDate)
            .Where(e => query.ToDate == null || e.Date <= query.ToDate)
            .Where(e => query.Source == null || e.Source == query.Source)
            .Where(e => query.IsArchived == null || e.IsArchived == query.IsArchived);

        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var cursor = EntryCursorDto.Decode(query.Cursor);
            if (cursor is not null)
            {
                entriesQuery = entriesQuery.Where(e => 
                    e.Date < cursor.Date || 
                    e.Date == cursor.Date && string.Compare(e.Id, cursor.Id) <= 0);
            }
        }

        List<EntryDto> entries = await entriesQuery
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Id)
            .Take(query.Limit + 1)
            .Select(EntryQueries.ProjectToDto())
            .ToListAsync();

        bool hasNextPage = entries.Count > query.Limit;
        string? nextCursor = null;
        if (hasNextPage)
        {
            EntryDto lastEntry = entries[^1];
            nextCursor = EntryCursorDto.Encode(lastEntry.Id, lastEntry.Date);
            entries.RemoveAt(entries.Count - 1);
        }

        var paginationResult = new CollectionResponse<ExpandoObject>
        {
            Items = dataShapingService.ShapeCollectionData(
                    entries, 
                    query.Fields,
                    query.IncludLinks ? 
                        e => CreateLinksForEntry(e.Id, query.Fields, e.IsArchived) : 
                        null),
        };

        if (query.IncludLinks)
        {
            paginationResult.Links = CreateLinksForEntriesCursor(
                query,
                nextCursor);
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
    // [IdempotentRequest]
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

    [HttpGet("stats")]
    public async Task<ActionResult<EntryStatsDto>> GetStats()
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var entries = await dbContext.Entries
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Date)
            .Select(e => new { e.Date })
            .ToListAsync();

        if (!entries.Any())
        {
            return Ok(new EntryStatsDto
            {
                DailyStats = [],
                TotalEntries = 0,
                CurrentStreak = 0,
                LongestStreak = 0
            });
        }

        int totalEntries = entries.Count;
        var dailyStats = entries
            .GroupBy(e => e.Date)
            .Select(g => new DailyStatsDto 
            { 
                Date = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(g => g.Date)
            .ToList();


        int longestStreak = 0;
        int currentStreak = 0;
        int currentCount = 0;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dates = entries.Select(e => e.Date).Distinct().OrderBy(e => e).ToList();

        for (int i = dates.Count - 1; i >= 0; i--)
        {
            if (i == dates.Count - 1)
            {
                if (dates[i] == today)
                {
                    currentStreak = 1;
                }
                else
                {
                    break;
                }
            }
            else if (dates[i + 1] == dates[i].AddDays(1))
            {
                currentStreak++;
            }
            else
            {
                break;
            }
        }

        for (int i = 0; i < dates.Count; i++)
        {
            if (i == 0 || dates[i] == dates[i - 1].AddDays(1))
            {
                currentCount++;
                longestStreak = Math.Max(longestStreak, currentCount);
            }
            else
            {
                currentCount = 0;
            }
        }

        return Ok(new EntryStatsDto
        {
            DailyStats = dailyStats,
            TotalEntries = totalEntries,
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak
        });
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

    private List<LinkDto> CreateLinksForEntriesCursor(EntriesCursorQueryParameters query, string? nextCursor)
    {
        List<LinkDto> links = 
        [
            linkService.Create(nameof(GetEntriesCursor), "self", HttpMethods.Get, new 
            {
                cursor = query.Cursor,
                limit = query.Limit,
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

        if (!string.IsNullOrWhiteSpace(nextCursor))
        {
            links.Add(linkService.Create(nameof(GetEntriesCursor), "next-page", HttpMethods.Get, new 
            {
                cursor = nextCursor,
                limit = query.Limit,
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
