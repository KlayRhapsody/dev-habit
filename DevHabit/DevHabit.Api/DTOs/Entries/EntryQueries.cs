using System.Linq.Expressions;
using DevHabit.Api.Entities;

namespace DevHabit.Api.DTOs.Entries;

public static class EntryQueries
{
    public static Expression<Func<Entry, EntryDto>> ProjectToDto()
    {
        return e => new EntryDto
        {
            Id = e.Id,
            Value = e.Value,
            Notes = e.Notes,
            Source = e.Source,
            ExternalId = e.ExternalId,
            IsArchived = e.IsArchived,
            Date = e.Date,
            CreatedAtUtc = e.CreatedAtUtc,
            UpdatedAtUtc = e.UpdatedAtUtc,
            Habit = new EntryHabitDto
            {
                Id = e.Habit.Id,
                Name = e.Habit.Name
            }
        };
    }
}
