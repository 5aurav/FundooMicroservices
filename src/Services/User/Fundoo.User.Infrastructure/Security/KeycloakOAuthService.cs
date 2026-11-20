using Fundoo.User.Application.Abstractions;
using Fundoo.User.Application.DTO;
using Fundoo.User.Domain.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Fundoo.User.Infrastructure.Security;

public class KeycloakOAuthService : IOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakSettings _settings;
    private readonly UserDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<KeycloakOAuthService> _logger;

    public KeycloakOAuthService(
        HttpClient httpClient,
        IOptions<KeycloakSettings> options,
        UserDbContext context,
        IUserRepository userRepository,
        ITokenService tokenService,
        ILogger<KeycloakOAuthService> logger)
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
        var targetRedirectUri = redirectUri ?? _settings.RedirectUri;
        var authEndpoint = $"{_settings.Authority.TrimEnd('/')}/protocol/openid-connect/auth";

        return $"{authEndpoint}?client_id={Uri.EscapeDataString(_settings.ClientId)}" +
               $"&response_type=code" +
               $"&scope=openid%20profile%20email" +
               $"&redirect_uri={Uri.EscapeDataString(targetRedirectUri)}" +
               $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<AuthResult> HandleCallbackAsync(
        string code,
        string? redirectUri = null,
        CancellationToken cancellationToken = default)
    {
        var targetRedirectUri = redirectUri ?? _settings.RedirectUri;

        try
        {
            var userInfo = await ExchangeCodeAndGetUserInfoAsync(code, targetRedirectUri, cancellationToken);
            if (userInfo == null || string.IsNullOrWhiteSpace(userInfo.Subject))
            {
                return new AuthResult { IsSuccess = false, Message = "Failed to obtain user identity from Keycloak." };
            }

            var identity = await _context.FederatedIdentities
                .FirstOrDefaultAsync(f => f.Provider == "Keycloak" && f.ProviderSubjectId == userInfo.Subject, cancellationToken);

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
                // Account Linking by Email
                var normalizedEmail = userInfo.Email.Trim().ToUpperInvariant();
                user = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

                if (user != null)
                {
                    // Link existing account
                    var newIdentity = new FederatedIdentity
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        Provider = "Keycloak",
                        ProviderSubjectId = userInfo.Subject,
                        Email = userInfo.Email,
                        CreatedAtUtc = DateTime.UtcNow,
                        LastLoginAtUtc = DateTime.UtcNow
                    };
                    await _context.FederatedIdentities.AddAsync(newIdentity, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Linked Keycloak identity {Sub} to existing user {UserId}", userInfo.Subject, user.Id);
                }
                else
                {
                    // Auto-provision new user
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
                        Provider = "Keycloak",
                        ProviderSubjectId = userInfo.Subject,
                        Email = userInfo.Email,
                        CreatedAtUtc = DateTime.UtcNow,
                        LastLoginAtUtc = DateTime.UtcNow
                    };
                    await _context.FederatedIdentities.AddAsync(newIdentity, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Auto-provisioned new user {UserId} from Keycloak identity {Sub}", user.Id, userInfo.Subject);
                }
            }

            var accessToken = _tokenService.CreateAccessToken(user);

            return new AuthResult
            {
                IsSuccess = true,
                Message = "External authentication successful.",
                UserId = user.Id,
                AccessToken = accessToken
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Keycloak OAuth callback.");
            return new AuthResult { IsSuccess = false, Message = "An error occurred during external login." };
        }
    }

    public async Task<bool> LinkAccountAsync(
        Guid userId,
        string code,
        string? redirectUri = null,
        CancellationToken cancellationToken = default)
    {
        var targetRedirectUri = redirectUri ?? _settings.RedirectUri;
        var userInfo = await ExchangeCodeAndGetUserInfoAsync(code, targetRedirectUri, cancellationToken);

        if (userInfo == null || string.IsNullOrWhiteSpace(userInfo.Subject))
        {
            return false;
        }

        var existingLink = await _context.FederatedIdentities
            .FirstOrDefaultAsync(f => f.Provider == "Keycloak" && f.ProviderSubjectId == userInfo.Subject, cancellationToken);

        if (existingLink != null)
        {
            return existingLink.UserId == userId; // Already linked to this user
        }

        var newIdentity = new FederatedIdentity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "Keycloak",
            ProviderSubjectId = userInfo.Subject,
            Email = userInfo.Email,
            CreatedAtUtc = DateTime.UtcNow,
            LastLoginAtUtc = DateTime.UtcNow
        };

        await _context.FederatedIdentities.AddAsync(newIdentity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<OAuthUserInfo?> ExchangeCodeAndGetUserInfoAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        var tokenEndpoint = $"{_settings.Authority.TrimEnd('/')}/protocol/openid-connect/token";

        var postParams = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "client_id", _settings.ClientId },
            { "code", code },
            { "redirect_uri", redirectUri }
        };

        if (!string.IsNullOrEmpty(_settings.ClientSecret))
        {
            postParams.Add("client_secret", _settings.ClientSecret);
        }

        var tokenResponse = await _httpClient.PostAsync(
            tokenEndpoint,
            new FormUrlEncodedContent(postParams),
            cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Keycloak token exchange failed ({Status}): {Body}", tokenResponse.StatusCode, errorBody);
            return null;
        }

        using var tokenDoc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();

        if (string.IsNullOrEmpty(accessToken)) return null;

        var userInfoEndpoint = $"{_settings.Authority.TrimEnd('/')}/protocol/openid-connect/userinfo";
        using var request = new HttpRequestMessage(HttpMethod.Get, userInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userInfoResponse = await _httpClient.SendAsync(request, cancellationToken);
        if (!userInfoResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Keycloak userinfo retrieval failed ({Status})", userInfoResponse.StatusCode);
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
            PreferredUsername = root.TryGetProperty("preferred_username", out var pref) ? pref.GetString() ?? "" : "",
            Provider = "Keycloak"
        };
    }
}
