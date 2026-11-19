
using Fundoo.Notes.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fundoo.Notes.Api.HealthChecks;

public class NotesDatabaseHealthCheck : IHealthCheck
{
    private readonly NotesDbConnectionFactory _connectionFactory;

    public NotesDatabaseHealthCheck(
        NotesDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection =
                _connectionFactory.CreateConnection();

            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";

            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy(
                "Notes database is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Notes database is unavailable.",
                exception);
        }
    }
}
