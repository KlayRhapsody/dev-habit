using DevHabit.Api.Entities;
using DevHabit.Api.Services.Sorting;

namespace DevHabit.Api.DTOs.Entries;

public static class EntryMappings
{
    public static readonly SortMappingDefinition<EntryDto, Entry> SortMapping = new ()
    {
        Mappings = 
        [
            new SortMapping(nameof(EntryDto.Date), nameof(Entry.Date)),
            new SortMapping(nameof(EntryDto.CreatedAtUtc), nameof(Entry.CreatedAtUtc)),
        ]
    };

    public static Entry ToEntity(this CreateEntryDto dto, string userId, Habit habit)
    {
        return new Entry
        {
            Id = $"e_{Guid.CreateVersion7()}",
            HabitId = dto.HabitId,
            UserId = userId,
            Value = dto.Value,
            Notes = dto.Note,
            Date = dto.Date,
            Source = EntrySource.Manual,
            CreatedAtUtc = DateTime.UtcNow,
            Habit = habit
        };
    }

    public static EntryDto ToDto(this Entry entry)
    {
        return new EntryDto
        {
            Id = entry.Id,
            Value = entry.Value,
            Notes = entry.Notes,
            Source = entry.Source,
            ExternalId = entry.ExternalId,
            IsArchived = entry.IsArchived,
            Date = entry.Date,
            CreatedAtUtc = entry.CreatedAtUtc,
            UpdatedAtUtc = entry.UpdatedAtUtc,
            Habit = new EntryHabitDto
            {
                Id = entry.Habit.Id,
                Name = entry.Habit.Name
            }
        };
    }

    public static void UpdateFromDto(this Entry entry, UpdateEntryDto dto)
    {
        entry.Value = dto.Value;
        entry.Notes = dto.Notes;
        entry.Date = dto.Date;
        entry.UpdatedAtUtc = DateTime.UtcNow;
    }
}
