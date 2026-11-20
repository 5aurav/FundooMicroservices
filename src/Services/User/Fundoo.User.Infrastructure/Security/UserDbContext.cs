using Fundoo.User.Domain.Entity;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.User.Infrastructure.Security
{
    public class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
    {
        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<FederatedIdentity> FederatedIdentities => Set<FederatedIdentity>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<AppUser>(e =>
            {
                e.HasKey(user => user.Id);
                e.HasIndex(u => u.NormalizedEmail).IsUnique();
                e.Property(u => u.Email).HasMaxLength(256);
                e.Property(u => u.NormalizedEmail).HasMaxLength(256);

                e.Property(u => u.PasswordHashVersion)
                    .IsRequired(false)
                    .HasDefaultValue(null);
            });

            b.Entity<PasswordResetToken>(e =>
            {
                e.HasKey(token => token.Id);

                e.Property(token => token.TokenHash)
                    .IsRequired()
                    .HasMaxLength(64);

                e.HasIndex(token => token.TokenHash)
                    .IsUnique();

                e.HasIndex(token => token.UserId);

                e.Property(token => token.ExpiresAtUtc)
                    .IsRequired();

                e.HasOne<AppUser>()
                    .WithMany()
                    .HasForeignKey(token => token.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<RefreshToken>(e =>
            {
                e.HasKey(token => token.Id);

                e.Property(token => token.TokenHash)
                    .IsRequired()
                    .HasMaxLength(64);

                e.HasIndex(token => token.FamilyId);

                e.Property(token => token.ReplacedByTokenHash)
                    .HasMaxLength(64);

                e.HasIndex(token => token.UserId);

                e.Property(token => token.CreatedAtUtc)
                    .IsRequired();

                e.Property(token => token.ExpiresAtUtc)
                    .IsRequired();

                e.HasOne<AppUser>()
                    .WithMany()
                    .HasForeignKey(token => token.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.Ignore(token => token.IsRevoked);
                e.Ignore(token => token.IsExpired);
                e.Ignore(token => token.IsActive);
            });

            b.Entity<OutboxMessage>(e =>
            {
                e.HasKey(m => m.Id);
                e.Property(m => m.EventType).IsRequired().HasMaxLength(128);
                e.Property(m => m.Payload).IsRequired();
                e.Property(m => m.CorrelationId).HasMaxLength(128);
                e.Property(m => m.CreatedAtUtc).IsRequired();
                e.HasIndex(m => m.ProcessedAtUtc);
            });

            b.Entity<FederatedIdentity>(e =>
            {
                e.HasKey(f => f.Id);
                e.Property(f => f.Provider).IsRequired().HasMaxLength(50);
                e.Property(f => f.ProviderSubjectId).IsRequired().HasMaxLength(256);
                e.Property(f => f.Email).HasMaxLength(256);
                e.HasIndex(f => new { f.Provider, f.ProviderSubjectId }).IsUnique();
                e.HasIndex(f => f.UserId);
            });

            b.Entity<UserSession>(e =>
            {
                e.HasKey(s => s.Id);
                e.Property(s => s.SessionTokenHash).IsRequired().HasMaxLength(64);
                e.HasIndex(s => s.SessionTokenHash).IsUnique();
                e.HasIndex(s => s.UserId);
                e.Property(s => s.IpAddress).HasMaxLength(45);
                e.Property(s => s.UserAgent).HasMaxLength(512);
                e.Property(s => s.DeviceName).HasMaxLength(100);
            });
        }
    }
}
