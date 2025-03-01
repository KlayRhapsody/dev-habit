using DevHabit.Api.Entities;

namespace DevHabit.Api.DTOs.Habits;

internal static class HabitMappings
{
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

    public static Habit ToEntity(this CreateHabitDto dto)
    {
        var habit = new Habit
        {
            Id = $"h_{Guid.CreateVersion7()}",
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
}
