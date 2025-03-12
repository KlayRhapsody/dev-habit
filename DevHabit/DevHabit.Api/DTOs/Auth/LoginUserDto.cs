namespace DevHabit.Api.DTOs.Auth;

public sealed record LoginUserDto
{
    public string Email { get; init; }
    public string Password { get; init; }
}
