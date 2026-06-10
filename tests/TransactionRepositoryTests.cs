using Microsoft.Data.Sqlite;
using src;

namespace tests;

public class TransactionRepositoryTests
{
    [Fact]
    public async Task UpsertAsync_InsertsAndReturnsCreatedRecord()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await TransactionRepository.EnsureSchemaAsync(connection);

        var request = new TransactionWebhookRequest(
            TransactionId: "txn-sqlite-1",
            Amount: 100.00m,
            Currency: "USD",
            Type: "payment",
            Merchant: "Test Merchant",
            OccurredAt: DateTime.UtcNow
        );

        var result = await TransactionRepository.UpsertAsync(connection, request);

        Assert.True(result.Inserted);
        Assert.Equal(request.TransactionId, result.Record.TransactionId);
        Assert.Equal(1.50m, result.Record.Fee);
        Assert.Equal(98.50m, result.Record.NetAmount);
        Assert.False(result.Record.HighValue);
    }

    [Fact]
    public async Task UpsertAsync_ReturnsExistingRecordWhenDuplicateTransactionId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await TransactionRepository.EnsureSchemaAsync(connection);

        var request = new TransactionWebhookRequest(
            TransactionId: "txn-sqlite-2",
            Amount: 2000.00m,
            Currency: "USD",
            Type: "refund",
            Merchant: "Test Merchant",
            OccurredAt: DateTime.UtcNow
        );

        var first = await TransactionRepository.UpsertAsync(connection, request);
        var second = await TransactionRepository.UpsertAsync(connection, request);

        Assert.True(first.Inserted);
        Assert.False(second.Inserted);
        Assert.Equal(first.Record.Id, second.Record.Id);
        Assert.Equal(first.Record.TransactionId, second.Record.TransactionId);
    }
}
