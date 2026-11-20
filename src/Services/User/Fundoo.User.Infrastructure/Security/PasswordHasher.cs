using Fundoo.User.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Fundoo.User.Infrastructure.Security;

/// <summary>
/// Hybrid password hasher supporting BCrypt (legacy) and PBKDF2 (current).
/// Always hashes new passwords with PBKDF2. Verifies both BCrypt ($2a/$2b/$2y prefix)
/// and PBKDF2 (no prefix). Use <see cref="NeedsRehash"/> to detect BCrypt hashes
/// during login and trigger lazy migration to PBKDF2.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _pbkdf2Hasher = new();

    public string Hash(string password)
    {
        // Always hash new passwords with PBKDF2
        return _pbkdf2Hasher.HashPassword(null!, password);
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        // BCrypt hashes start with $2a$, $2b$, or $2y$
        if (IsBCryptHash(hash))
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }

        // PBKDF2 hash
        var result = _pbkdf2Hasher.VerifyHashedPassword(
            null!,
            hash,
            password);

        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded;
    }

    /// <summary>
    /// Returns true if the hash was created with BCrypt and should be upgraded
    /// to PBKDF2 on the next successful login.
    /// </summary>
    public bool NeedsRehash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        return IsBCryptHash(hash);
    }

    private static bool IsBCryptHash(string hash) =>
        hash.StartsWith("$2a$", StringComparison.Ordinal) ||
        hash.StartsWith("$2b$", StringComparison.Ordinal) ||
        hash.StartsWith("$2y$", StringComparison.Ordinal);
}
