using System;

namespace tests;

public class TransactionCalculationsTests
{
    [Fact]
    public void CalculateFee_ReturnsRoundedFee()
    {
        var fee = TransactionCalculations.CalculateFee(123.45m);

        Assert.Equal(1.85m, fee);
    }

    [Fact]
    public void BuildOutputRecord_ComputesNetAmountAndHighValueFlag()
    {
        var request = new TransactionWebhookRequest(
            TransactionId: "txn-123",
            Amount: 2000m,
            Currency: "NGN",
            Type: "payment",
            Merchant: "Ade Store",
            OccurredAt: new DateTime(2026, 06, 10, 12, 0, 0, DateTimeKind.Utc)
        );

        var result = TransactionCalculations.BuildOutputRecord(request);

        Assert.Equal(2000m, result.Amount);
        Assert.Equal(30.00m, result.Fee);
        Assert.Equal(1970.00m, result.NetAmount);
        Assert.True(result.HighValue);
        Assert.Equal(request.TransactionId, result.TransactionId);
    }
}
