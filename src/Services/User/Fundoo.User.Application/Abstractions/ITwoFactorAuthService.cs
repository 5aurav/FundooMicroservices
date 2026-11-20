namespace Fundoo.User.Application.Abstractions;

public interface ITwoFactorAuthService
{
    Task<(string Secret, string QrCodeUri, IEnumerable<string> RecoveryCodes)> InitiateTwoFactorSetupAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> EnableTwoFactorAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> DisableTwoFactorAsync(
        Guid userId,
        string codeOrRecoveryCode,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyTwoFactorCodeAsync(
        Guid userId,
        string codeOrRecoveryCode,
        CancellationToken cancellationToken = default);
}
