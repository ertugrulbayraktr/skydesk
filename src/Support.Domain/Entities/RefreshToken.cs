using Support.Domain.Common;

namespace Support.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    // SHA-256 of the opaque token; the raw token is only ever returned to the client
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private RefreshToken() { } // EF Core

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public bool IsActive(DateTime utcNow) => RevokedAt == null && ExpiresAt > utcNow;

    public void Revoke(DateTime utcNow)
    {
        if (RevokedAt == null)
        {
            RevokedAt = utcNow;
            UpdateTimestamp();
        }
    }
}
