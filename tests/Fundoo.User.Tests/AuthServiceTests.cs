using FluentAssertions;
using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entity;
using Fundoo.User.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Fundoo.User.Tests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IHttpClientFactory> _httpFactoryMock = new();
    private readonly Mock<ILogger<AuthService>> _loggerMock = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _userRepoMock.Object,
            _hasherMock.Object,
            _tokenServiceMock.Object,
            _httpFactoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ReturnsFailure()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByNormalizedEmailAsync("TEST@EXAMPLE.COM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppUser { Email = "test@example.com" });

        // Act
        var result = await _sut.RegisterAsync("John", "Doe", "test@example.com", "Password123!");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("already registered");
    }

    [Fact]
    public async Task RegisterAsync_WhenValid_QueuesOutboxMessageAndSaves()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByNormalizedEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);
        _hasherMock.Setup(h => h.Hash(It.IsAny<string>()))
            .Returns("hashedPassword");
        _tokenServiceMock.Setup(t => t.CreateAccessToken(It.IsAny<AppUser>()))
            .Returns("mockJwt");

        // Act
        var result = await _sut.RegisterAsync("John", "Doe", "new@example.com", "Password123!");

        // Assert
        result.IsSuccess.Should().BeTrue();
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Once);
        _userRepoMock.Verify(r => r.AddOutboxMessageAsync(It.Is<OutboxMessage>(m => m.EventType == "UserRegisteredEvent"), It.IsAny<CancellationToken>()), Times.Once);
        _userRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserExists_AddsResetTokenAndOutboxMessage()
    {
        // Arrange
        var user = new AppUser { Id = Guid.NewGuid(), Email = "user@example.com", FirstName = "Alice", IsActive = true };
        _userRepoMock.Setup(r => r.GetByNormalizedEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.ForgotPasswordAsync("user@example.com");

        // Assert
        _userRepoMock.Verify(r => r.AddPasswordResetTokenAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _userRepoMock.Verify(r => r.AddOutboxMessageAsync(It.Is<OutboxMessage>(m => m.EventType == "PasswordResetRequestedEvent"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
