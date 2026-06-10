using src;

namespace tests;

public class WebhookSecurityTests
{
    [Fact]
    public void ComputeSignature_ReturnsSha256PrefixedHex()
    {
        const string payload = "{\"transaction_id\":\"txn-1\"}";
        const string secret = "super-secret";

        var signature = WebhookSecurity.ComputeSignature(payload, secret);

        Assert.StartsWith("sha256=", signature);
        Assert.Equal(71, signature.Length);
    }

    [Fact]
    public void IsValidSignature_ReturnsTrueForMatchingSignature()
    {
        const string payload = "{\"transaction_id\":\"txn-1\"}";
        const string secret = "super-secret";
        var signature = WebhookSecurity.ComputeSignature(payload, secret);

        Assert.True(WebhookSecurity.IsValidSignature(payload, signature, secret));
    }

    [Fact]
    public void IsValidSignature_ReturnsFalseForMismatch()
    {
        const string payload = "{\"transaction_id\":\"txn-1\"}";
        const string secret = "super-secret";
        var signature = "sha256=invalidsignature";

        Assert.False(WebhookSecurity.IsValidSignature(payload, signature, secret));
    }
}
