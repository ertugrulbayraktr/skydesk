using Support.Domain.Common;
using Support.Domain.Enums;

namespace Support.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public Role Role { get; private set; }
    public bool IsActive { get; private set; }
    
    // For Passengers - optional PNR for quick lookup
    public string? LastKnownPNR { get; private set; }

    // Account lockout (agents/admins with password login)
    public int FailedLoginCount { get; private set; }
    public DateTime? LockedOutUntil { get; private set; }

    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private User() { } // EF Core

    public User(string email, string passwordHash, string fullName, Role role)
    {
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        Role = role;
        IsActive = true;
    }

    /// <summary>
    /// Creates a passenger account provisioned from PNR verification.
    /// Passengers never log in with a password; the hash is empty by design.
    /// </summary>
    public static User CreatePassenger(string email, string fullName, string pnr)
    {
        var user = new User(email, passwordHash: "", fullName, Role.Passenger);
        user.LastKnownPNR = pnr;
        return user;
    }

    public bool IsLockedOut(DateTime utcNow) => LockedOutUntil.HasValue && LockedOutUntil.Value > utcNow;

    public void RegisterFailedLogin(DateTime utcNow)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedLogins)
        {
            LockedOutUntil = utcNow.Add(LockoutDuration);
            FailedLoginCount = 0;
        }
        UpdateTimestamp();
    }

    public void ResetLockout()
    {
        FailedLoginCount = 0;
        LockedOutUntil = null;
        UpdateTimestamp();
    }

    public void UpdateLastKnownPNR(string pnr)
    {
        LastKnownPNR = pnr;
        UpdateTimestamp();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdateTimestamp();
    }
}
