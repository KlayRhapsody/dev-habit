namespace DevHabit.Api.DTOs.Entries;

public sealed record EntryHabitDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}
