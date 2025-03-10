using System.Linq.Expressions;
using System.Linq.Dynamic.Core;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevHabit.Api.Services.Sorting;
using OpenTelemetry.Trace;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.Services;
using System.Dynamic;
using Asp.Versioning;

namespace DevHabit.Api.Controllers;

[ApiController]
[Route("habits")]
[ApiVersion("1.0")]
public sealed class HabitsController(ApplicationDbContext dbContext, LinkService linkService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHabits(
        [FromQuery] HabitsQueryParameters query,
        SortMappingProvider sortMappingProvider,
        DataShapingService dataShapingService)
    {
        if (!sortMappingProvider.ValidateMappings<HabitDto, Habit>(query.Sort))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided sort parameter isn't valid: '{query.Sort}'");
        }

        if (!dataShapingService.Validate<Habit>(query.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided fields parameter isn't valid: '{query.Fields}'");
        }

        query.Search = query.Search?.Trim().ToLower();

        SortMapping[] sortMapping = sortMappingProvider.GetMappings<HabitDto, Habit>();

        IQueryable<HabitDto> habitsQuery = dbContext.Habits
            .Where(h => query.Search == null ||
                h.Name.ToLower().Contains(query.Search) ||
                h.Description != null && h.Description.ToLower().Contains(query.Search))
            .Where(h => query.Type == null || h.Type == query.Type)
            .Where(h => query.Status == null || h.Status == query.Status)
            .ApplySort(query.Sort, sortMapping)
            .Select(HabitQueries.ProjectToDto());

        int totalCount = await habitsQuery.CountAsync();
        List<HabitDto> items = await habitsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        bool includeLinks = query.Accept == CustomMediaTypeNames.Application.HateoasJson;
        var paginationResult = new PaginationResult<ExpandoObject>
        {
            Item = dataShapingService.ShapeCollectionData(
                items,
                query.Fields,
                h => includeLinks ? CreateLinksForHabit(h.Id, query.Fields) : null),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
        };

        if (includeLinks)
        {
            paginationResult.Links = CreateLinksForHabits(
                query, 
                paginationResult.HasPreviousPage, 
                paginationResult.HasNextPage);
        }

        return Ok(paginationResult);
    }

    [MapToApiVersion("1.0")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetHabit(
        string id,
        string? fields,
        [FromHeader(Name = "Accept")]
        string? accept,
        DataShapingService dataShapingService)
    {
        if (!dataShapingService.Validate<HabitWithTagsDtoV1>(fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided fields parameter isn't valid: '{fields}'");
        }

        HabitWithTagsDtoV1 habit = await dbContext.Habits
            .Where(h => h.Id == id)
            .Select(HabitQueries.ProjectToDtoWithTagsV1())
            .FirstOrDefaultAsync();

        if (habit is null)
        {
            return NotFound();
        }

        ExpandoObject expandoObject = dataShapingService.ShapeData(habit, fields);

        if (accept == CustomMediaTypeNames.Application.HateoasJson)
        {
            List<LinkDto> links = CreateLinksForHabit(id, fields);
            expandoObject.TryAdd("links", links);
        }

        return Ok(expandoObject);
    }

    [ApiVersion("2.0")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetHabitV2(
        string id,
        string? fields,
        [FromHeader(Name = "Accept")]
        string? accept,
        DataShapingService dataShapingService)
    {
        if (!dataShapingService.Validate<HabitWithTagsDtoV2>(fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided fields parameter isn't valid: '{fields}'");
        }

        HabitWithTagsDtoV2 habit = await dbContext.Habits
            .Where(h => h.Id == id)
            .Select(HabitQueries.ProjectToDtoWithTagsV2())
            .FirstOrDefaultAsync();

        if (habit is null)
        {
            return NotFound();
        }

        ExpandoObject expandoObject = dataShapingService.ShapeData(habit, fields);

        if (accept == CustomMediaTypeNames.Application.HateoasJson)
        {
            List<LinkDto> links = CreateLinksForHabit(id, fields);
            expandoObject.TryAdd("links", links);
        }

        return Ok(expandoObject);
    }

    [HttpPost]
    public async Task<ActionResult<HabitDto>> CreateHabit(
        CreateHabitDto createHabitDto,
        IValidator<CreateHabitDto> validator)
    {
        await validator.ValidateAndThrowAsync(createHabitDto);

        Habit habit = createHabitDto.ToEntity();

        dbContext.Habits.Add(habit);

        await dbContext.SaveChangesAsync();

        HabitDto habitDto = habit.ToDto();
        habitDto.Links = CreateLinksForHabit(habitDto.Id, null);

        return CreatedAtAction(nameof(GetHabit), new { id = habitDto.Id }, habitDto);   
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateHabit(string id, UpdateHabitDto updateHabitDto)
    {
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id);

        if (habit is null)
        {
            return NotFound();
        }

        habit.UpdateFromDto(updateHabitDto);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult> PatchHabit(string id, JsonPatchDocument<HabitDto> jsonPatchDocument)
    {
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id);

        if (habit is null)
        {
            return NotFound();
        }

        HabitDto habitDto = habit.ToDto();

        jsonPatchDocument.ApplyTo(habitDto, ModelState);

        if (!TryValidateModel(habitDto))
        {
            return ValidationProblem(ModelState);
        }

        habit.Name = habitDto.Name;
        habit.Description = habitDto.Description;
        habit.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteHabit(string id)
    {
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id);

        if (habit is null)
        {
            // return StatusCode(StatusCodes.Status410Gone);
            return NotFound();
        }

        dbContext.Habits.Remove(habit);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private List<LinkDto> CreateLinksForHabits(
        HabitsQueryParameters parameters,
        bool hasPreviousPage,
        bool hasNextPage)
    {
        List<LinkDto> links =
        [
            linkService.Create(nameof(GetHabits), "self", HttpMethods.Get, new { 
                page = parameters.Page, 
                pageSize = parameters.PageSize, 
                fields = parameters.Fields,
                q = parameters.Search, 
                sort = parameters.Sort, 
                type = parameters.Type, 
                status = parameters.Status 
            }),
            linkService.Create(nameof(CreateHabit), "create", HttpMethods.Post)
        ];

        if (hasPreviousPage)
        {
            links.Add(linkService.Create(nameof(GetHabits), "previous-page", HttpMethods.Get, new { 
                page = parameters.Page - 1, 
                pageSize = parameters.PageSize, 
                fields = parameters.Fields,
                q = parameters.Search, 
                sort = parameters.Sort, 
                type = parameters.Type, 
                status = parameters.Status 
            }));
        }

        if (hasNextPage)
        {
            links.Add(linkService.Create(nameof(GetHabits), "next-page", HttpMethods.Get, new { 
                page = parameters.Page + 1, 
                pageSize = parameters.PageSize, 
                fields = parameters.Fields,
                q = parameters.Search, 
                sort = parameters.Sort, 
                type = parameters.Type, 
                status = parameters.Status 
            }));
        }

        return links;
    }

    private List<LinkDto> CreateLinksForHabit(string id, string? fields)
    {
        return 
        [
            linkService.Create(nameof(GetHabit), "self", HttpMethods.Get, new { id, fields }),
            linkService.Create(nameof(UpdateHabit), "update", HttpMethods.Put, new { id }),
            linkService.Create(nameof(PatchHabit), "partial-update", HttpMethods.Patch, new { id }),
            linkService.Create(nameof(DeleteHabit), "delete", HttpMethods.Delete, new { id }),
            linkService.Create(
                nameof(HabitTagsController.UpsertHabitTags), 
                "upsert-tags", 
                HttpMethods.Put,
                new { habitId = id},
                HabitTagsController.Name)
        ];
    }
}
