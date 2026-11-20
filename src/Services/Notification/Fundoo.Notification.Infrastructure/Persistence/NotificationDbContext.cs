using Fundoo.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext(
    DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationIdempotency> NotificationIdempotencies =>
        Set<NotificationIdempotency>();

    public DbSet<NotificationDelivery> NotificationDeliveries =>
        Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationIdempotency>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique();

            entity.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();
        });

        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.IdempotencyKey).HasMaxLength(100);
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();

            entity.Property(x => x.Type).HasConversion<int>().IsRequired();
            entity.Property(x => x.Status).HasConversion<int>().IsRequired();
            entity.Property(x => x.RecipientEmail).IsRequired().HasMaxLength(256);
            entity.Property(x => x.RecipientName).HasMaxLength(200);
            entity.Property(x => x.FailureReason).HasMaxLength(2000);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        });
    }
}
