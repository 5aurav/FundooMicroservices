namespace Fundoo.User.Api.Settings;

public sealed class NotificationClientSettings
{
    public const string SectionName = "Services:Notification";

    public string BaseUrl { get; set; } = string.Empty;
    public int AttemptTimeoutSeconds { get; set; } = 2;
    public int TotalTimeoutSeconds { get; set; } = 8;
    public int MaxRetryAttempts { get; set; } = 2;
    public int RetryBaseDelayMs { get; set; } = 500;

    public void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException(
                $"{SectionName}:BaseUrl must be an absolute URL.");

        if (AttemptTimeoutSeconds is < 1 or > 51)
            throw new InvalidOperationException(
                $"{SectionName}:AttemptTimeoutSeconds must be 1-15.");

        if (TotalTimeoutSeconds <= AttemptTimeoutSeconds)
            throw new InvalidOperationException(
                $"{SectionName}:TotalTimeoutSeconds must be greater than AttemptTimeoutSeconds.");

        if (MaxRetryAttempts is < 1 or > 5)
            throw new InvalidOperationException(
                $"{SectionName}:MaxRetryAttempts must be 1-5.");

        if (RetryBaseDelayMs < 50)
            throw new InvalidOperationException(
                $"{SectionName}:RetryBaseDelayMs must be at least 50.");
    }
}