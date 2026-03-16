using System.Security.Cryptography;
using System.Text;
using KasahQMS.Application.Common.Interfaces.Services;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// TOTP-based two-factor authentication service (RFC 6238).
/// </summary>
public class TwoFactorService : ITwoFactorService
{
    private const int SecretKeyLength = 20;
    private const int TimeStepSeconds = 30;
    private const int CodeDigits = 6;
    private static readonly string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecretKey()
    {
        var bytes = new byte[SecretKeyLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base32Encode(bytes);
    }

    public string GenerateQrCodeUri(string email, string secretKey)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        return $"otpauth://totp/KasahQMS:{encodedEmail}?secret={secretKey}&issuer=KasahQMS";
    }

    public bool ValidateCode(string secretKey, string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != CodeDigits)
            return false;

        var keyBytes = Base32Decode(secretKey);
        var currentTimeStep = GetCurrentTimeStep();

        // Check current step and ±1 window
        for (long i = -1; i <= 1; i++)
        {
            var computedCode = ComputeTotp(keyBytes, currentTimeStep + i);
            if (computedCode == code)
                return true;
        }

        return false;
    }

    public List<string> GenerateRecoveryCodes(int count = 8)
    {
        var codes = new List<string>(count);
        using var rng = RandomNumberGenerator.Create();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        for (int i = 0; i < count; i++)
        {
            var codeBytes = new byte[8];
            rng.GetBytes(codeBytes);
            var code = new char[8];
            for (int j = 0; j < 8; j++)
                code[j] = chars[codeBytes[j] % chars.Length];
            codes.Add(new string(code));
        }

        return codes;
    }

    private static long GetCurrentTimeStep()
    {
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return unixTime / TimeStepSeconds;
    }

    private static string ComputeTotp(byte[] key, long timeStep)
    {
        var timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timeBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(timeBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binaryCode % (int)Math.Pow(10, CodeDigits);
        return otp.ToString().PadLeft(CodeDigits, '0');
    }

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder();
        int buffer = 0, bitsLeft = 0;

        foreach (var b in data)
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

    private static byte[] Base32Decode(string base32)
    {
        var bytes = new List<byte>();
        int buffer = 0, bitsLeft = 0;

        foreach (var c in base32.ToUpperInvariant())
        {
            var index = Base32Chars.IndexOf(c);
            if (index < 0) continue;

            buffer = (buffer << 5) | index;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)(buffer >> bitsLeft));
            }
        }

        return bytes.ToArray();
    }
}
