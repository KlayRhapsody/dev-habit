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

namespace DevHabit.Api.Controllers;

[ApiController]
[Route("habits")]
public sealed class HabitsController(ApplicationDbContext dbContext) : ControllerBase
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

        var paginationResult = new PaginationResult<ExpandoObject>
        {
            Item = dataShapingService.ShapeCollectionData(items, query.Fields),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };

        return Ok(paginationResult);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<HabitWithTagsDto>> GetHabit(string id)
    {
        HabitWithTagsDto habit = await dbContext.Habits
            .Where(h => h.Id == id)
            .Select(HabitQueries.ProjectToDtoWithTags())
            .FirstOrDefaultAsync();

        if (habit is null)
        {
            return NotFound();
        }

        return Ok(habit);
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
}
