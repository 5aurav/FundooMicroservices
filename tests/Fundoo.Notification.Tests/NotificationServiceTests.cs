using FluentAssertions;
using Fundoo.Notification.Api.Services;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.DTO;
using Fundoo.Notification.Domain.Entities;
using Fundoo.Notification.Domain.Enums;
using Moq;
using Xunit;

namespace Fundoo.Notification.Tests;

public class NotificationServiceTests
{
    private readonly Mock<INotificationIdempotencyRepository> _idempotencyRepoMock = new();
    private readonly Mock<INotificationDeliveryRepository> _deliveryRepoMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        _sut = new NotificationService(
            _idempotencyRepoMock.Object,
            _deliveryRepoMock.Object,
            _emailSenderMock.Object);
    }

    [Fact]
    public async Task SendPasswordReset_WhenDuplicateKeyAlreadySent_ShortCircuitsWithoutSending()
    {
        // Arrange
        var request = new PasswordResetNotificationRequest
        {
            IdempotencyKey = "existing-key-123",
            RecipientEmail = "test@example.com",
            RecipientName = "Bob",
            ResetToken = "tokenABC"
        };

        _idempotencyRepoMock.Setup(r => r.TryCreatePendingAsync(request.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _idempotencyRepoMock.Setup(r => r.GetStatusAsync(request.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationStatus.Sent);

        // Act
        await _sut.SendPasswordResetNotificationAsync(request);

        // Assert
        _emailSenderMock.Verify(e => e.SendPasswordResetNotificationAsync(It.IsAny<PasswordResetNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _deliveryRepoMock.Verify(d => d.AddAsync(It.IsAny<NotificationDelivery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendPasswordReset_WhenNewKey_SendsEmailAndMarksSent()
    {
        // Arrange
        var request = new PasswordResetNotificationRequest
        {
            IdempotencyKey = "new-key-456",
            RecipientEmail = "test@example.com",
            RecipientName = "Bob",
            ResetToken = "tokenABC"
        };

        _idempotencyRepoMock.Setup(r => r.TryCreatePendingAsync(request.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.SendPasswordResetNotificationAsync(request);

        // Assert
        _emailSenderMock.Verify(e => e.SendPasswordResetNotificationAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        _idempotencyRepoMock.Verify(r => r.MarkSentAsync(request.IdempotencyKey, It.IsAny<CancellationToken>()), Times.Once);
        _deliveryRepoMock.Verify(d => d.MarkSentAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
