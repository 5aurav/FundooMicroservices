using Fundoo.Notification.Infrastructure.Persistence;
using Fundoo.User.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

Console.WriteLine("=== Fundoo Microservices Migration Runner ===");

var userDbConn = Environment.GetEnvironmentVariable("ConnectionStrings__UserDb")
    ?? "Host=localhost;Port=5432;Database=FundooUserDb;Username=postgres;Password=postgres";

var notifDbConn = Environment.GetEnvironmentVariable("ConnectionStrings__NotificationDb")
    ?? "Host=localhost;Port=5433;Database=FundooNotificationDb;Username=postgres;Password=postgres";

var notesDbConn = Environment.GetEnvironmentVariable("ConnectionStrings__NotesDb")
    ?? "Host=localhost;Port=5434;Database=FundooNotesDb;Username=postgres;Password=postgres";

await RunWithRetryAsync("User Service Database Migrations", async () =>
{
    var options = new DbContextOptionsBuilder<UserDbContext>()
        .UseNpgsql(userDbConn)
        .Options;

    await using var context = new UserDbContext(options);
    await context.Database.MigrateAsync();
    Console.WriteLine("✓ User DB migrations applied successfully.");
});

await RunWithRetryAsync("Notification Service Database Migrations", async () =>
{
    var options = new DbContextOptionsBuilder<NotificationDbContext>()
        .UseNpgsql(notifDbConn)
        .Options;

    await using var context = new NotificationDbContext(options);
    await context.Database.MigrateAsync();
    Console.WriteLine("✓ Notification DB migrations applied successfully.");
});

await RunWithRetryAsync("Notes Service Database Migrations (SQL script)", async () =>
{
    var sqlPath = Path.Combine(AppContext.BaseDirectory, "001_initial_schema.sql");
    string sqlScript;
    if (File.Exists(sqlPath))
    {
        sqlScript = await File.ReadAllTextAsync(sqlPath);
    }
    else
    {
        // Fallback default DDL
        sqlScript = @"
CREATE TABLE IF NOT EXISTS notes (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    title VARCHAR(255) NOT NULL,
    content TEXT NOT NULL,
    color VARCHAR(50),
    is_pinned BOOLEAN NOT NULL DEFAULT FALSE,
    is_archived BOOLEAN NOT NULL DEFAULT FALSE,
    is_trash BOOLEAN NOT NULL DEFAULT FALSE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS labels (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    name VARCHAR(100) NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS note_labels (
    note_id UUID NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
    label_id UUID NOT NULL REFERENCES labels(id) ON DELETE CASCADE,
    PRIMARY KEY (note_id, label_id)
);";
    }

    await using var conn = new NpgsqlConnection(notesDbConn);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(sqlScript, conn);
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine("✓ Notes DB schema applied successfully.");
});

Console.WriteLine("=== All Database Migrations Completed Successfully ===");

static async Task RunWithRetryAsync(string operationName, Func<Task> action, int maxRetries = 10, int delaySeconds = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            Console.WriteLine($"Running {operationName} (Attempt {attempt}/{maxRetries})...");
            await action();
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Attempt {attempt} failed] {ex.Message}");
            if (attempt == maxRetries) throw;
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }
    }
}
