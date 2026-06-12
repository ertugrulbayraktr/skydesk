using System.Security.Cryptography;

namespace Support.Application.Common;

public static class RefreshTokenGenerator
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>Generates a cryptographically random opaque token (returned to the client raw).</summary>
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>SHA-256 hex of the raw token — only the hash is persisted.</summary>
    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
}
