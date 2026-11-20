using Fundoo.User.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fundoo.User.Infrastructure.Security;

public class TotpTwoFactorAuthService : ITwoFactorAuthService
{
    private readonly UserDbContext _context;
    private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public TotpTwoFactorAuthService(UserDbContext context)
    {
        _context = context;
    }

    public async Task<(string Secret, string QrCodeUri, IEnumerable<string> RecoveryCodes)> InitiateTwoFactorSetupAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var rawSecretBytes = RandomNumberGenerator.GetBytes(20);
        var secret = ToBase32String(rawSecretBytes);

        var plainRecoveryCodes = Enumerable.Range(0, 8)
            .Select(_ => $"{GenerateRandomCode(4)}-{GenerateRandomCode(4)}")
            .ToList();

        var hashedRecoveryCodes = plainRecoveryCodes
            .Select(code => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))))
            .ToList();

        user.TwoFactorSecret = secret;
        user.TwoFactorRecoveryCodes = JsonSerializer.Serialize(hashedRecoveryCodes);
        await _context.SaveChangesAsync(cancellationToken);

        var qrCodeUri = $"otpauth://totp/Fundoo:{Uri.EscapeDataString(user.Email)}?secret={secret}&issuer=Fundoo&algorithm=SHA1&digits=6&period=30";

        return (secret, qrCodeUri, plainRecoveryCodes);
    }

    public async Task<bool> EnableTwoFactorAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null || string.IsNullOrEmpty(user.TwoFactorSecret)) return false;

        if (!ValidateTotp(user.TwoFactorSecret, code))
        {
            return false;
        }

        user.IsTwoFactorEnabled = true;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DisableTwoFactorAsync(
        Guid userId,
        string codeOrRecoveryCode,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null || !user.IsTwoFactorEnabled) return false;

        var isValid = await VerifyTwoFactorCodeAsync(userId, codeOrRecoveryCode, cancellationToken);
        if (!isValid) return false;

        user.IsTwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorRecoveryCodes = null;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> VerifyTwoFactorCodeAsync(
        Guid userId,
        string codeOrRecoveryCode,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null || !user.IsTwoFactorEnabled) return false;

        var cleanInput = codeOrRecoveryCode.Trim();

        // 1. Try TOTP code
        if (cleanInput.Length == 6 && int.TryParse(cleanInput, out _) && !string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            if (ValidateTotp(user.TwoFactorSecret, cleanInput))
            {
                return true;
            }
        }

        // 2. Try Recovery code
        if (!string.IsNullOrEmpty(user.TwoFactorRecoveryCodes))
        {
            var hashedCodes = JsonSerializer.Deserialize<List<string>>(user.TwoFactorRecoveryCodes) ?? new List<string>();
            var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cleanInput)));

            if (hashedCodes.Contains(inputHash))
            {
                // Single-use: remove consumed recovery code
                hashedCodes.Remove(inputHash);
                user.TwoFactorRecoveryCodes = JsonSerializer.Serialize(hashedCodes);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
        }

        return false;
    }

    private static bool ValidateTotp(string base32Secret, string code)
    {
        var secretBytes = FromBase32String(base32Secret);
        var currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        // Tolerance window of ±1 step (30 seconds before / after)
        for (var step = currentStep - 1; step <= currentStep + 1; step++)
        {
            if (ComputeTotp(secretBytes, step) == code)
            {
                return true;
            }
        }

        return false;
    }

    private static string ComputeTotp(byte[] secretBytes, long step)
    {
        var stepBytes = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(stepBytes);
        }

        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(stepBytes);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                   | ((hash[offset + 1] & 0xFF) << 16)
                   | ((hash[offset + 2] & 0xFF) << 8)
                   | (hash[offset + 3] & 0xFF);

        var otp = binary % 1_000_000;
        return otp.ToString("D6");
    }

    private static string ToBase32String(byte[] bytes)
    {
        var sb = new StringBuilder();
        int buffer = 0, bitsLeft = 0;

        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Chars[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
        {
            buffer <<= (5 - bitsLeft);
            sb.Append(Base32Chars[buffer & 0x1F]);
        }

        return sb.ToString();
    }

    private static byte[] FromBase32String(string base32)
    {
        var clean = base32.Trim().ToUpperInvariant();
        var bytes = new List<byte>();
        int buffer = 0, bitsLeft = 0;

        foreach (var c in clean)
        {
            var val = Base32Chars.IndexOf(c);
            if (val < 0) continue;

            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return bytes.ToArray();
    }

    private static string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }
        return new string(result);
    }
}
