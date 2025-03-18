using DevHabit.Api.DTOs.Common;
using DevHabit.Api.Entities;

namespace DevHabit.Api.DTOs.Entries;

public sealed record EntryDto : ILinksResponse
{
    public required string Id { get; set; }
    public required int Value { get; set; }
    public string? Notes { get; set; }
    public required EntrySource Source { get; init; }
    public string? ExternalId { get; init; }
    public required bool IsArchived { get; set; }
    public required DateOnly Date { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public EntryHabitDto Habit { get; set; }
    public List<LinkDto> Links { get; set; }
}
