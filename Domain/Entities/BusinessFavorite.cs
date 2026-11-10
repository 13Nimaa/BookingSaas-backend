namespace Domain.Entities;

/// <summary>
/// Customer (or any authenticated user) favorite salon. Unique per (UserId, BusinessId).
/// </summary>
public class BusinessFavorite
{
    public int Id { get; set; }

    public string UserId { get; set; } = default!;
    public ApplicationUser User { get; set; } = default!;

    public int BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
