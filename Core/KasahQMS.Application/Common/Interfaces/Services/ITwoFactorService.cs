namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for two-factor authentication operations using TOTP (RFC 6238).
/// </summary>
public interface ITwoFactorService
{
    string GenerateSecretKey();
    string GenerateQrCodeUri(string email, string secretKey);
    bool ValidateCode(string secretKey, string code);
    List<string> GenerateRecoveryCodes(int count = 8);
}
