namespace DevHabit.Api.DTOs.Habits;

public sealed record HabitCollectionDto
{
    public List<HabitDto> Data { get; init; }
}
