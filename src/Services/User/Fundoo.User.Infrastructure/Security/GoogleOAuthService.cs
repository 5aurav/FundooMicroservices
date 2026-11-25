using Fundoo.User.Application.Abstractions;
using Fundoo.User.Application.DTO;
using Fundoo.User.Domain.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Fundoo.User.Infrastructure.Security;

public class GoogleOAuthService : IGoogleOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly GoogleOAuthSettings _settings;
    private readonly UserDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<GoogleOAuthService> _logger;

    public GoogleOAuthService(
        HttpClient httpClient,
        IOptions<GoogleOAuthSettings> options,
        UserDbContext context,
        IUserRepository userRepository,
        ITokenService tokenService,
        ILogger<GoogleOAuthService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _context = context;
        _userRepository = userRepository;
        _tokenService = tokenService;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string state, string? redirectUri = null)
    {
        var targetRedirectUri = !string.IsNullOrWhiteSpace(redirectUri) ? redirectUri : _settings.RedirectUri;
        const string authEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

        return $"{authEndpoint}?client_id={Uri.EscapeDataString(_settings.ClientId)}" +
               $"&response_type=code" +
               $"&scope=openid%20profile%20email" +
               $"&redirect_uri={Uri.EscapeDataString(targetRedirectUri)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&access_type=offline" +
               $"&prompt=consent";
    }

    public async Task<AuthResult> HandleCallbackAsync(
        string code,
        string? redirectUri = null,
        CancellationToken cancellationToken = default)
    {
        var targetRedirectUri = !string.IsNullOrWhiteSpace(redirectUri) ? redirectUri : _settings.RedirectUri;

        try
        {
            var userInfo = await ExchangeCodeAndGetUserInfoAsync(code, targetRedirectUri, cancellationToken);
            if (userInfo == null || string.IsNullOrWhiteSpace(userInfo.Subject))
            {
                return new AuthResult { IsSuccess = false, Message = "Failed to obtain user identity from Google." };
            }

            return await ProcessOAuthUserAsync(userInfo, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Google OAuth callback.");
            return new AuthResult { IsSuccess = false, Message = "An error occurred during Google login." };
        }
    }

    public async Task<AuthResult> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenInfoEndpoint = $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}";
            var response = await _httpClient.GetAsync(tokenInfoEndpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Google id_token validation failed ({Status}): {Body}", response.StatusCode, errorBody);
                return new AuthResult { IsSuccess = false, Message = "Invalid Google ID token." };
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = doc.RootElement;

            var userInfo = new OAuthUserInfo
            {
                Subject = root.TryGetProperty("sub", out var sub) ? sub.GetString() ?? "" : "",
                Email = root.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "",
                GivenName = root.TryGetProperty("given_name", out var given) ? given.GetString() ?? "" : "",
                FamilyName = root.TryGetProperty("family_name", out var family) ? family.GetString() ?? "" : "",
                Provider = "Google"
            };

            if (string.IsNullOrWhiteSpace(userInfo.Subject) || string.IsNullOrWhiteSpace(userInfo.Email))
            {
                return new AuthResult { IsSuccess = false, Message = "Google ID token did not contain subject or email." };
            }

            return await ProcessOAuthUserAsync(userInfo, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Google ID token.");
            return new AuthResult { IsSuccess = false, Message = "An error occurred during Google ID token authentication." };
        }
    }

    public async Task<bool> LinkAccountAsync(
        Guid userId,
        string code,
        string? redirectUri = null,
        CancellationToken cancellationToken = default)
    {
        var targetRedirectUri = !string.IsNullOrWhiteSpace(redirectUri) ? redirectUri : _settings.RedirectUri;
        var userInfo = await ExchangeCodeAndGetUserInfoAsync(code, targetRedirectUri, cancellationToken);

        if (userInfo == null || string.IsNullOrWhiteSpace(userInfo.Subject))
        {
            return false;
        }

        var existingLink = await _context.FederatedIdentities
            .FirstOrDefaultAsync(f => f.Provider == "Google" && f.ProviderSubjectId == userInfo.Subject, cancellationToken);

        if (existingLink != null)
        {
            return existingLink.UserId == userId;
        }

        var newIdentity = new FederatedIdentity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "Google",
            ProviderSubjectId = userInfo.Subject,
            Email = userInfo.Email,
            CreatedAtUtc = DateTime.UtcNow,
            LastLoginAtUtc = DateTime.UtcNow
        };

        await _context.FederatedIdentities.AddAsync(newIdentity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<AuthResult> ProcessOAuthUserAsync(OAuthUserInfo userInfo, CancellationToken cancellationToken)
    {
        var identity = await _context.FederatedIdentities
            .FirstOrDefaultAsync(f => f.Provider == "Google" && f.ProviderSubjectId == userInfo.Subject, cancellationToken);

        AppUser? user = null;

        if (identity != null)
        {
            user = await _userRepository.GetByIdAsync(identity.UserId, cancellationToken);
            if (user == null || !user.IsActive)
            {
                return new AuthResult { IsSuccess = false, Message = "Linked user account is inactive or not found." };
            }

            identity.LastLoginAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var normalizedEmail = userInfo.Email.Trim().ToUpperInvariant();
            user = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

            if (user != null)
            {
                var newIdentity = new FederatedIdentity
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Provider = "Google",
                    ProviderSubjectId = userInfo.Subject,
                    Email = userInfo.Email,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastLoginAtUtc = DateTime.UtcNow
                };
                await _context.FederatedIdentities.AddAsync(newIdentity, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Linked Google identity {Sub} to existing user {UserId}", userInfo.Subject, user.Id);
            }
            else
            {
                user = new AppUser
                {
                    Id = Guid.NewGuid(),
                    FirstName = string.IsNullOrWhiteSpace(userInfo.GivenName) ? "User" : userInfo.GivenName,
                    LastName = string.IsNullOrWhiteSpace(userInfo.FamilyName) ? "" : userInfo.FamilyName,
                    Email = userInfo.Email,
                    NormalizedEmail = normalizedEmail,
                    PasswordHash = "",
                    PasswordHashVersion = 2,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                await _userRepository.AddAsync(user, cancellationToken);

                var newIdentity = new FederatedIdentity
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Provider = "Google",
                    ProviderSubjectId = userInfo.Subject,
                    Email = userInfo.Email,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastLoginAtUtc = DateTime.UtcNow
                };
                await _context.FederatedIdentities.AddAsync(newIdentity, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Auto-provisioned new user {UserId} from Google identity {Sub}", user.Id, userInfo.Subject);
            }
        }

        var accessToken = _tokenService.CreateAccessToken(user);

        return new AuthResult
        {
            IsSuccess = true,
            Message = "Google authentication successful.",
            UserId = user.Id,
            AccessToken = accessToken
        };
    }

    private async Task<OAuthUserInfo?> ExchangeCodeAndGetUserInfoAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        const string tokenEndpoint = "https://oauth2.googleapis.com/token";

        var postParams = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "client_id", _settings.ClientId },
            { "client_secret", _settings.ClientSecret },
            { "code", code },
            { "redirect_uri", redirectUri }
        };

        var tokenResponse = await _httpClient.PostAsync(
            tokenEndpoint,
            new FormUrlEncodedContent(postParams),
            cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Google token exchange failed ({Status}): {Body}", tokenResponse.StatusCode, errorBody);
            return null;
        }

        using var tokenDoc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();

        if (string.IsNullOrEmpty(accessToken)) return null;

        const string userInfoEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";
        using var request = new HttpRequestMessage(HttpMethod.Get, userInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userInfoResponse = await _httpClient.SendAsync(request, cancellationToken);
        if (!userInfoResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Google userinfo retrieval failed ({Status})", userInfoResponse.StatusCode);
            return null;
        }

        using var userDoc = JsonDocument.Parse(await userInfoResponse.Content.ReadAsStringAsync(cancellationToken));
        var root = userDoc.RootElement;

        return new OAuthUserInfo
        {
            Subject = root.TryGetProperty("sub", out var sub) ? sub.GetString() ?? "" : "",
            Email = root.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "",
            GivenName = root.TryGetProperty("given_name", out var given) ? given.GetString() ?? "" : "",
            FamilyName = root.TryGetProperty("family_name", out var family) ? family.GetString() ?? "" : "",
            Provider = "Google"
        };
    }
}
