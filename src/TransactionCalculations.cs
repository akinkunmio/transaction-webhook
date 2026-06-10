public static class TransactionCalculations
{
    public static decimal CalculateFee(decimal amount)
    {
        return Math.Round(amount * 0.015m, 2, MidpointRounding.AwayFromZero);
    }

    public static TransactionRecord BuildOutputRecord(TransactionWebhookRequest request)
    {
        var fee = CalculateFee(request.Amount);
        var netAmount = request.Amount - fee;
        var highValue = request.Amount >= 1000m;

        return new TransactionRecord
        {
            Id = 0,
            TransactionId = request.TransactionId,
            Amount = request.Amount,
            Currency = request.Currency,
            Type = request.Type,
            Merchant = request.Merchant,
            OccurredAt = request.OccurredAt,
            ProcessedAt = DateTime.UtcNow,
            Fee = fee,
            NetAmount = netAmount,
            HighValue = highValue
        };
    }
}
