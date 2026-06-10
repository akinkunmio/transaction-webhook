using System.Data;
using System.Text.Json;
using Dapper;

namespace src;

public sealed record TransactionUpsertResult(TransactionRecord Record, bool Inserted);

public static class TransactionRepository
{
    private const string SqliteInsertSql = @"
INSERT OR IGNORE INTO transactions
    (transaction_id, amount, currency, transaction_type, merchant_name, occurred_at, raw_payload, fee, net_amount, high_value)
VALUES
    (@TransactionId, @Amount, @Currency, @Type, @Merchant, @OccurredAt, @RawPayload, @Fee, @NetAmount, @HighValue);";

    private const string SqliteSelectSql = "SELECT id, transaction_id AS TransactionId, amount AS Amount, currency AS Currency, transaction_type AS Type, merchant_name AS Merchant, occurred_at AS OccurredAt, processed_at AS ProcessedAt, fee AS Fee, net_amount AS NetAmount, high_value AS HighValue FROM transactions WHERE transaction_id = @TransactionId";

    private const string PostgresInsertSql = @"
INSERT INTO transactions
    (transaction_id, amount, currency, transaction_type, merchant_name, occurred_at, raw_payload, fee, net_amount, high_value)
VALUES
    (@TransactionId, @Amount, @Currency, @Type, @Merchant, @OccurredAt, @RawPayload, @Fee, @NetAmount, @HighValue)
ON CONFLICT (transaction_id) DO NOTHING
RETURNING id, transaction_id AS TransactionId, amount AS Amount, currency AS Currency,
          transaction_type AS Type, merchant_name AS Merchant, occurred_at AS OccurredAt,
          processed_at AS ProcessedAt, fee AS Fee, net_amount AS NetAmount, high_value AS HighValue;";

    private const string PostgresSelectSql = SqliteSelectSql;

    private const string SqliteCreateSql = @"
CREATE TABLE IF NOT EXISTS transactions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    transaction_id TEXT NOT NULL UNIQUE,
    amount REAL NOT NULL,
    currency TEXT NOT NULL,
    transaction_type TEXT NOT NULL,
    merchant_name TEXT NOT NULL,
    occurred_at TEXT NOT NULL,
    raw_payload TEXT NOT NULL,
    fee REAL NOT NULL,
    net_amount REAL NOT NULL,
    high_value INTEGER NOT NULL,
    processed_at TEXT NOT NULL DEFAULT (datetime('now'))
);";

    public static async Task<TransactionUpsertResult> UpsertAsync(IDbConnection connection, TransactionWebhookRequest request)
    {
        var fee = TransactionCalculations.CalculateFee(request.Amount);
        var netAmount = request.Amount - fee;
        var highValue = request.Amount >= 1000m;
        var rawPayload = JsonSerializer.Serialize(request);

        if (IsSqlite(connection))
        {
            var rows = await connection.ExecuteAsync(SqliteInsertSql, new
            {
                request.TransactionId,
                request.Amount,
                request.Currency,
                request.Type,
                request.Merchant,
                request.OccurredAt,
                RawPayload = rawPayload,
                Fee = fee,
                NetAmount = netAmount,
                HighValue = highValue ? 1 : 0
            });

            var sqliteRecord = await connection.QuerySingleAsync<TransactionRecord>(SqliteSelectSql, new { request.TransactionId });

            return new TransactionUpsertResult(sqliteRecord, rows > 0);
        }

        var inserted = await connection.QuerySingleOrDefaultAsync<TransactionRecord>(PostgresInsertSql, new
        {
            request.TransactionId,
            request.Amount,
            request.Currency,
            request.Type,
            request.Merchant,
            request.OccurredAt,
            RawPayload = rawPayload,
            Fee = fee,
            NetAmount = netAmount,
            HighValue = highValue
        });

        if (inserted is not null)
            return new TransactionUpsertResult(inserted, true);

        var record = await connection.QuerySingleAsync<TransactionRecord>(PostgresSelectSql, new { request.TransactionId });

        return new TransactionUpsertResult(record, false);
    }

    public static async Task EnsureSchemaAsync(IDbConnection connection)
    {
        if (!IsSqlite(connection))
            return;

        await connection.ExecuteAsync(SqliteCreateSql);
    }

    static bool IsSqlite(IDbConnection connection)
        => connection.GetType().FullName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
}
