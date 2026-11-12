using Fundoo.Notification.Application.DTO;
using Fundoo.Notification.Application.Templates;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Fundoo.Notification.Application.Abstractions;

namespace Fundoo.Notification.Api.Services;

public sealed class SmtpEmailService(IConfiguration configuration) : IEmailSender
{
    public async Task SendPasswordResetNotificationAsync(
    PasswordResetNotificationRequest request,
    CancellationToken cancellationToken = default)
    {
        var body = PasswordResetEmailTemplate.Build(
            string.IsNullOrWhiteSpace(request.RecipientName)
                ? "User"
                : request.RecipientName,
            request.ResetToken);

        await SendEmailAsync(
            request.RecipientEmail,
            "Fundoo Notes - Password Reset",
            body,
            cancellationToken);
    }

    public async Task SendWelcomeNotificationAsync(
        WelcomeNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var body = WelcomeEmailTemplate.Build(
            request.RecipientName);

        await SendEmailAsync(
            request.RecipientEmail,
            "Welcome to Fundoo Notes!",
            body,
            cancellationToken);
    }

    private async Task SendEmailAsync(
    string recipientEmail,
    string subject,
    string body,
    CancellationToken cancellationToken)
    { 
        var host = GetRequiredSetting("Host");

        if (!int.TryParse(
                GetRequiredSetting("Port"),
                out var port))
        {
            throw new InvalidOperationException(
                "EmailSettings:Port must be a valid number.");
        }

        var senderEmail = GetRequiredSetting("SenderEmail");
        var senderName = configuration["EmailSettings:SenderName"]
                         ?? "Fundoo Notes";

        var username = GetRequiredSetting("Username");
        var password = GetRequiredSetting("Password");

        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress(senderName, senderEmail));

        message.To.Add(
            MailboxAddress.Parse(recipientEmail));

        message.Subject = subject;

        message.Body = new TextPart("html")
        {
            Text = body
        };

        using var client = new SmtpClient();
        client.Timeout = 5000;

        await client.ConnectAsync(
            host,
            port,
            SecureSocketOptions.StartTls,
            cancellationToken);

        try
        {
            await client.AuthenticateAsync(
                username,
                password,
                cancellationToken);

            await client.SendAsync(
                message,
                cancellationToken);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true);
            }
        }
    }

    private string GetRequiredSetting(string key)
    {
        return configuration[$"EmailSettings:{key}"]
               ?? throw new InvalidOperationException(
                   $"EmailSettings:{key} is missing.");
    }
}
