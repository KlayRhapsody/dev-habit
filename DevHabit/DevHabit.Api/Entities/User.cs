namespace DevHabit.Api.Entities;

public sealed class User
{
    public string Id { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAtUTC { get; set; }
    public DateTime? UpdatedAtUTC { get; set; }

    public string IdentityId { get; set; }

    public static string NewId() => $"u_{Guid.CreateVersion7()}";
}
