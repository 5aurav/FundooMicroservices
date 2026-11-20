using System;
using System.ComponentModel.DataAnnotations;

namespace Fundoo.User.Domain.Entity
{
    public class AppUser
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string NormalizedEmail { get; set; } = "";
        public string PasswordHash { get; set; } = "";

        /// <summary>
        /// Tracks the password-hashing algorithm version. 1 = BCrypt (legacy),
        /// 2 = PBKDF2 (current). Null indicates BCrypt for backward compatibility
        /// with existing records created before this migration.
        /// </summary>
        public int? PasswordHashVersion { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsTwoFactorEnabled { get; set; } = false;
        public string? TwoFactorSecret { get; set; }
        public string? TwoFactorRecoveryCodes { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}