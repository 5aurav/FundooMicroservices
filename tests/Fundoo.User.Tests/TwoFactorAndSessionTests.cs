using FluentAssertions;
using Fundoo.User.Domain.Entity;
using Fundoo.User.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fundoo.User.Tests;

public class TwoFactorAndSessionTests
{
    private UserDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new UserDbContext(options);
    }

    [Fact]
    public async Task TwoFactor_SetupAndEnable_WhenValidCode_EnablesSuccessfully()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "totp@test.com",
            IsActive = true
        };
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var sut = new TotpTwoFactorAuthService(context);

        // Act
        var (secret, qrCodeUri, recoveryCodes) = await sut.InitiateTwoFactorSetupAsync(user.Id);

        // Assert
        secret.Should().NotBeNullOrEmpty();
        qrCodeUri.Should().Contain("otpauth://totp");
        recoveryCodes.Should().HaveCount(8);

        user.IsTwoFactorEnabled = true;
        await context.SaveChangesAsync();

        // Verify recovery code login
        var firstRecoveryCode = recoveryCodes.First();
        var isRecoveryValid = await sut.VerifyTwoFactorCodeAsync(user.Id, firstRecoveryCode);
        isRecoveryValid.Should().BeTrue();

        // Recovery code should be single-use
        var isReusedValid = await sut.VerifyTwoFactorCodeAsync(user.Id, firstRecoveryCode);
        isReusedValid.Should().BeFalse();
    }

    [Fact]
    public async Task SessionService_CreateAndRevoke_WorksAsExpected()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var sut = new SessionService(context);

        // Act - Create Session
        var (session, rawToken) = await sut.CreateSessionAsync(userId, "127.0.0.1", "Mozilla/5.0 (Windows NT 10.0)");

        // Assert
        session.Should().NotBeNull();
        rawToken.Should().NotBeNullOrEmpty();

        var activeSessions = await sut.GetActiveSessionsAsync(userId);
        activeSessions.Should().ContainSingle();

        // Act - Revoke Session
        var revoked = await sut.RevokeSessionAsync(userId, session.Id);
        revoked.Should().BeTrue();

        var activeAfterRevoke = await sut.GetActiveSessionsAsync(userId);
        activeAfterRevoke.Should().BeEmpty();
    }
}
