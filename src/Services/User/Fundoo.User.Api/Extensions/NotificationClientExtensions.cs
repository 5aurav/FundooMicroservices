using Fundoo.User.Api.Settings;
using Microsoft.Extensions.Http.Resilience;

namespace Fundoo.User.Api.Extensions;

public static class NotificationClientExtensions
{
    // Must match the name AuthService passes to IHttpClientFactory.CreateClient(...)
    public const string ClientName = "NotificationService";

    public static IServiceCollection AddNotificationClient(
        this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(NotificationClientSettings.SectionName)
            .Get<NotificationClientSettings>() ?? new NotificationClientSettings();

        settings.Validate();

        services
            .AddHttpClient(ClientName, client =>
            {
                client.BaseAddress = new Uri(settings.BaseUrl);
                // Polly owns timeouts; HttpClient's own timeout must not interfere.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler(options =>
            {
                options.RateLimiter.DefaultRateLimiterOptions.PermitLimit = 10;
                options.RateLimiter.DefaultRateLimiterOptions.QueueLimit = 0;
                options.RateLimiter.DefaultRateLimiterOptions.QueueProcessingOrder =
                    System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                // Timeouts: one per attempt, plus an overall budget across retries.
                options.AttemptTimeout.Timeout =
                    TimeSpan.FromSeconds(settings.AttemptTimeoutSeconds);
                options.TotalRequestTimeout.Timeout =
                    TimeSpan.FromSeconds(settings.TotalTimeoutSeconds);

                // Retry with exponential backoff + jitter (spreads out retry storms).
                options.Retry.MaxRetryAttempts = settings.MaxRetryAttempts;
                options.Retry.Delay = TimeSpan.FromMilliseconds(settings.RetryBaseDelayMs);
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
            });

        return services;
    }
}