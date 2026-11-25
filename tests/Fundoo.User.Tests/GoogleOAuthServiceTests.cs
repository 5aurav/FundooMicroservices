using FluentAssertions;
using Fundoo.User.Application.Abstractions;
using Fundoo.User.Application.DTO;
using Fundoo.User.Domain.Entity;
using Fundoo.User.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Fundoo.User.Tests;

public class GoogleOAuthServiceTests
{
    private readonly UserDbContext _dbContext;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<GoogleOAuthService>> _loggerMock;
    private readonly GoogleOAuthSettings _settings;

    public GoogleOAuthServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(databaseName: $"GoogleOAuthDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new UserDbContext(dbOptions);
        _userRepositoryMock = new Mock<IUserRepository>();
        _tokenServiceMock = new Mock<ITokenService>();
        _loggerMock = new Mock<ILogger<GoogleOAuthService>>();

        _settings = new GoogleOAuthSettings
        {
            ClientId = "test-google-client-id",
            ClientSecret = "test-google-secret",
            RedirectUri = "http://localhost:7000/api/v1/auth/google/callback"
        };
    }

    [Fact]
    public void GetAuthorizationUrl_ShouldGenerateValidGoogleAuthUrl()
    {
        // Arrange
        var optionsMock = Options.Create(_settings);
        var httpClient = new HttpClient();
        var service = new GoogleOAuthService(
            httpClient,
            optionsMock,
            _dbContext,
            _userRepositoryMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);

        var state = "secure-random-state-123";

        // Act
        var url = service.GetAuthorizationUrl(state);

        // Assert
        url.Should().StartWith("https://accounts.google.com/o/oauth2/v2/auth");
        url.Should().Contain("client_id=test-google-client-id");
        url.Should().Contain("response_type=code");
        url.Should().Contain("scope=openid%20profile%20email");
        url.Should().Contain("state=secure-random-state-123");
        url.Should().Contain("redirect_uri=http%3A%2F%2Flocalhost%3A7000%2Fapi%2Fv1%2Fauth%2Fgoogle%2Fcallback");
    }

    [Fact]
    public void GetAuthorizationUrl_WithCustomRedirectUri_ShouldUseCustomRedirectUri()
    {
        // Arrange
        var optionsMock = Options.Create(_settings);
        var httpClient = new HttpClient();
        var service = new GoogleOAuthService(
            httpClient,
            optionsMock,
            _dbContext,
            _userRepositoryMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);

        var customRedirect = "http://myvm.eastus.cloudapp.azure.com:7000/api/v1/auth/google/callback";

        // Act
        var url = service.GetAuthorizationUrl("test-state", customRedirect);

        // Assert
        url.Should().Contain(Uri.EscapeDataString(customRedirect));
    }
}
