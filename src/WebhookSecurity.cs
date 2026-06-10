using System.Security.Cryptography;
using System.Text;

namespace src;

public static class WebhookSecurity
{
    public static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool IsValidSignature(string payload, string? signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        var expectedPrefix = "sha256=";
        if (!signatureHeader.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var expected = ComputeSignature(payload, secret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signatureHeader),
            Encoding.UTF8.GetBytes(expected));
    }
}
