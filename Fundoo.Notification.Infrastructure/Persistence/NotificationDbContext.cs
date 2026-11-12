using Fundoo.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext(
    DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationIdempotency> NotificationIdempotencies =>
        Set<NotificationIdempotency>();

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
    }
}