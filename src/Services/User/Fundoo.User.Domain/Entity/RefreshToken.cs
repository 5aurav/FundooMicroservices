namespace Fundoo.User.Domain.Entity
{
    public class RefreshToken
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        // Store the SHA-256 hash, never the original refresh token.
        public string TokenHash { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime? RevokedAtUtc { get; set; }

        // Hash of the replacement token when token rotation occurs.
        public string? ReplacedByTokenHash { get; set; }

        public bool IsRevoked => RevokedAtUtc.HasValue;

        public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

        public bool IsActive => !IsRevoked && !IsExpired;
    }
}