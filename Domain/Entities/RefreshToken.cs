using Domain.Entities;

namespace Domain.Entities;
public class RefreshToken
{
    public int Id {get; set;}
    public required string Token {get; set;}
    public required string UserId {get; set;}
    public required ApplicationUser User {get; set;}
    public DateTimeOffset CreatedAt {get; set;}
    public DateTimeOffset ExpiresAt {get; set;}
    public DateTimeOffset? RevokedAt {get; set;}
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;


}