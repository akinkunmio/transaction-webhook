using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using src;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("HealthCheck")
    .WithDescription("Returns 200 if the service is running.")
    .Produces(StatusCodes.Status200OK);

app.MapPost("/webhooks/transactions", async (HttpContext context, IConfiguration config, ILogger<Program> logger) =>
{
    const string signatureHeader = "X-Webhook-Signature";
    const string webhookSecretKey = "WEBHOOK_SECRET";

    var requestBody = await ReadRequestBodyAsync(context.Request);
    if (requestBody.Length == 0)
    {
        logger.LogWarning("Empty webhook request body received.");
        return Results.BadRequest(new { error = "request body is required" });
    }

    var webhookSecret = config.GetValue<string>(webhookSecretKey);
    if (!string.IsNullOrWhiteSpace(webhookSecret))
    {
        var signature = context.Request.Headers[signatureHeader].FirstOrDefault();
        if (!WebhookSecurity.IsValidSignature(requestBody, signature, webhookSecret))
        {
            logger.LogWarning("Webhook signature validation failed.");
            return Results.Unauthorized();
        }
    }

    TransactionWebhookRequest? request;
    try
    {
        request = JsonSerializer.Deserialize<TransactionWebhookRequest>(requestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
    catch (JsonException ex)
    {
        logger.LogWarning(ex, "Webhook payload deserialization failed.");
        return Results.BadRequest(new { error = "invalid request payload" });
    }

    if (request is null)
    {
        logger.LogWarning("Webhook payload was empty or malformed.");
        return Results.BadRequest(new { error = "invalid request payload" });
    }

    if (string.IsNullOrWhiteSpace(request.TransactionId))
        return Results.BadRequest(new { error = "transaction_id is required" });

    if (request.Amount <= 0)
        return Results.BadRequest(new { error = "amount must be positive" });

    if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Length != 3)
        return Results.BadRequest(new { error = "currency must be a three-letter code" });

    if (string.IsNullOrWhiteSpace(request.Type))
        return Results.BadRequest(new { error = "type is required" });

    if (string.IsNullOrWhiteSpace(request.Merchant))
        return Results.BadRequest(new { error = "merchant is required" });

    if (request.OccurredAt > DateTime.UtcNow.AddMinutes(5))
        return Results.BadRequest(new { error = "occurred_at cannot be in the future" });

    var defaultPostgres = config.GetConnectionString("Postgres")
        ?? "Host=localhost;Port=5432;Database=transactions;Username=postgres;Password=postgres";

    var connectionString = config.GetValue<string>("POSTGRES_CONNECTION")
        ?? defaultPostgres;

    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    var upsertResult = await TransactionRepository.UpsertAsync(connection, request);

    logger.LogInformation("Processed transaction {TransactionId} highValue={HighValue} inserted={Inserted}", request.TransactionId, upsertResult.Record.HighValue, upsertResult.Inserted);

    return upsertResult.Inserted
        ? Results.Created($"/webhooks/transactions/{upsertResult.Record.TransactionId}", upsertResult.Record)
        : Results.Ok(upsertResult.Record);
})
.WithName("PostTransaction")
.WithDescription("Receive and process a transaction webhook. Validates signature, payload, and stores transaction in database.")
.Produces<TransactionRecord>(StatusCodes.Status201Created)
.Produces<TransactionRecord>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status401Unauthorized);

app.Run();

static async Task<string> ReadRequestBodyAsync(HttpRequest request)
{
    request.EnableBuffering();
    await using var memoryStream = new MemoryStream();
    await request.Body.CopyToAsync(memoryStream);
    request.Body.Seek(0, SeekOrigin.Begin);
    return Encoding.UTF8.GetString(memoryStream.ToArray());
}

public sealed record TransactionWebhookRequest(
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("merchant")] string Merchant,
    [property: JsonPropertyName("occurred_at")] DateTime OccurredAt
);

public sealed class TransactionRecord
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("merchant")]
    public string Merchant { get; set; } = string.Empty;

    [JsonPropertyName("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [JsonPropertyName("processed_at")]
    public DateTime ProcessedAt { get; set; }

    [JsonPropertyName("fee")]
    public decimal Fee { get; set; }

    [JsonPropertyName("net_amount")]
    public decimal NetAmount { get; set; }

    [JsonPropertyName("high_value")]
    public bool HighValue { get; set; }
}
