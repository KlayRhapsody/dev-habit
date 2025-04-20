namespace DevHabit.Api.DTOs.Entries;

public record DailyStatsDto
{
    public required DateOnly Date { get; init; }
    public required int Count { get; init; }
}
