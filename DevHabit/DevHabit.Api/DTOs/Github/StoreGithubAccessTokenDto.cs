namespace DevHabit.Api.DTOs.Github;

public sealed record StoreGithubAccessTokenDto
{
    public required string AccessToken { get; init; }
    public required int ExpiresInDays { get; init; }
}
