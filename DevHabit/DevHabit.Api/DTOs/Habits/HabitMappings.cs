using DevHabit.Api.Entities;
using DevHabit.Api.Services.Sorting;

namespace DevHabit.Api.DTOs.Habits;

internal static class HabitMappings
{
    public static readonly SortMappingDefinition<HabitDto, Habit> SortMapping = new()
    {
        Mappings =
        [
            new SortMapping(nameof(HabitDto.Name), nameof(Habit.Name)),
            new SortMapping(nameof(HabitDto.Description), nameof(Habit.Description)),
            new SortMapping(nameof(HabitDto.Type), nameof(Habit.Type)),
            new SortMapping(
                $"{nameof(HabitDto.Frequency)}.{nameof(HabitDto.Frequency.Type)}",
                $"{nameof(Habit.Frequency)}.{nameof(Habit.Frequency.Type)}"),
            new SortMapping(
                $"{nameof(HabitDto.Frequency)}.{nameof(HabitDto.Frequency.TimesPerPeriod)}",
                $"{nameof(Habit.Frequency)}.{nameof(Habit.Frequency.TimesPerPeriod)}"),
            new SortMapping(
                $"{nameof(HabitDto.Target)}.{nameof(HabitDto.Target.Value)}",
                $"{nameof(Habit.Target)}.{nameof(Habit.Target.Value)}"),
            new SortMapping(
                $"{nameof(HabitDto.Target)}.{nameof(HabitDto.Target.Unit)}",
                $"{nameof(Habit.Target)}.{nameof(Habit.Target.Unit)}"),
            new SortMapping(nameof(HabitDto.Status), nameof(Habit.Status)),
            new SortMapping(nameof(HabitDto.EndedDate), nameof(Habit.EndedDate)),
            new SortMapping(nameof(HabitDto.Milestone), nameof(Habit.Milestone.Target)),
            new SortMapping(nameof(HabitDto.CreatedAtUtc), nameof(Habit.CreatedAtUtc)),
            new SortMapping(nameof(HabitDto.UpdatedAtUtc), nameof(Habit.UpdatedAtUtc)),
            new SortMapping(nameof(HabitDto.LastCompletedAtUtc), nameof(Habit.LastCompletedAtUtc))
        ]
    };

    public static HabitDto ToDto(this Habit habit)
    {
        var habitDto = new HabitDto
        {
            Id = habit.Id,
            Name = habit.Name,
            Description = habit.Description,
            Type = habit.Type,
            Frequency = new FrequencyDto
            {
                Type = habit.Frequency.Type,
                TimesPerPeriod = habit.Frequency.TimesPerPeriod
            },
            Target = new TargetDto
            {
                Value = habit.Target.Value,
                Unit = habit.Target.Unit
            },
            Status = habit.Status,
            IsArchived = habit.IsArchived,
            EndedDate = habit.EndedDate,
            Milestone = habit.Milestone == null ? null : new MilestoneDto
            {
                Target = habit.Milestone.Target,
                Current = habit.Milestone.Current
            },
            CreatedAtUtc = habit.CreatedAtUtc,
            UpdatedAtUtc = habit.UpdatedAtUtc,
            LastCompletedAtUtc = habit.LastCompletedAtUtc
        };

        return habitDto;
    }

    public static Habit ToEntity(this CreateHabitDto dto, string userId)
    {
        var habit = new Habit
        {
            Id = $"h_{Guid.CreateVersion7()}",
            UserId = userId,
            Name = dto.Name,
            Description = dto.Description,
            Type = dto.Type,
            Frequency = new Frequency
            {
                Type = dto.Frequency.Type,
                TimesPerPeriod = dto.Frequency.TimesPerPeriod
            },
            Target = new Target
            {
                Value = dto.Target.Value,
                Unit = dto.Target.Unit
            },
            Status = HabitStatus.Ongoing,
            IsArchived = false,
            EndedDate = dto.EndedDate,
            Milestone = dto.Milestone == null ? null : new Milestone
            {
                Target = dto.Milestone.Target,
                Current = 0
            },
            CreatedAtUtc = DateTime.UtcNow
        };

        return habit;
    }

    public static void UpdateFromDto(this Habit habit, UpdateHabitDto dto)
    {
        habit.Name = dto.Name;
        habit.Description = dto.Description;
        habit.Type = dto.Type;
        habit.EndedDate = dto.EndedDate;

        habit.Frequency = new Frequency
        {
            Type = dto.Frequency.Type,
            TimesPerPeriod = dto.Frequency.TimesPerPeriod
        };

        habit.Target = new Target
        {
            Value = dto.Target.Value,
            Unit = dto.Target.Unit
        };

        if (dto.Milestone is not null)
        {
            habit.Milestone ??= new Milestone();
            habit.Milestone.Target = dto.Milestone.Target;
        }
        
        habit.UpdatedAtUtc = DateTime.UtcNow;
    }
}
