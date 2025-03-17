using DevHabit.Api.DTOs.Common;

namespace DevHabit.Api.DTOs.Users;

public sealed record UserDto : ILinksResponse
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }

    public List<LinkDto> Links { get; set; }
}
