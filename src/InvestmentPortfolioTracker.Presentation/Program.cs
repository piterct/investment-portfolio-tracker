using InvestmentPortfolioTracker.Business.Models;
using InvestmentPortfolioTracker.Business.Repositories;
using InvestmentPortfolioTracker.Business.Services;
using InvestmentPortfolioTracker.Business.Shared;
using InvestmentPortfolioTracker.Repository.DataContext;
using InvestmentPortfolioTracker.Repository.Repositories;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.RegularExpressions;
using InvestmentPortfolioTracker.Business.Notifications;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient<IQuoteRefreshService, QuoteRefreshService>(client =>
{
    client.BaseAddress = new Uri("https://brapi.dev/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("InvestmentPortfolioTracker/1.0");
});

builder.Services.AddSingleton(new QuoteRefreshOptions
{
    FreshnessMinutes = builder.Configuration.GetValue<int?>("Quotes:FreshnessMinutes") ?? 2
});

builder.Services.Configure<Settings>(builder.Configuration.GetSection("Database"));
builder.Services.AddScoped<InvestmentPortfolioContext>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
builder.Services.AddScoped<IAssetRepository, AssetRepository>();
builder.Services.AddScoped<INotifier, Notifier>();
builder.Services.AddScoped<IPortfolioService, PortfolioService>();
builder.Services.AddControllers();
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
});

var app = builder.Build();
var configuredDirectory = app.Configuration["Database:Directory"];
var databasePath = string.IsNullOrWhiteSpace(configuredDirectory)
    ? app.Environment.ContentRootPath
    : Path.IsPathRooted(configuredDirectory)
        ? configuredDirectory
        : Path.Combine(app.Environment.ContentRootPath, configuredDirectory);

var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = databasePath,
    Mode = SqliteOpenMode.ReadWriteCreate,
    Cache = SqliteCacheMode.Shared
}.ToString();

await Database.InitializeAsync(connectionString);

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", database = Path.GetFileName(databasePath) }));

app.MapGet("/api/settings", async (ISettingsRepository settingsRepository) =>
{
    var settings = await settingsRepository.GetSettingsAsync();
    return Results.Ok(new { patrimony = settings.Patrimony, hasApiToken = !string.IsNullOrWhiteSpace(settings.ApiToken) });
});

app.MapPut("/api/settings", async (SettingsInput input, ISettingsRepository settingsRepository) =>
{
    if (input.Patrimony < 0)
        return Results.BadRequest(new { error = "O patrimônio de referência não pode ser negativo." });

    var existing = await settingsRepository.GetSettingsAsync();
    var token = input.ClearApiToken
        ? null
        : string.IsNullOrWhiteSpace(input.ApiToken)
            ? existing.ApiToken
            : input.ApiToken.Trim();

    await Database.SaveSettingsAsync(connectionString, input.Patrimony, token);
    return Results.Ok(new { patrimony = input.Patrimony, hasApiToken = !string.IsNullOrWhiteSpace(token) });
});

app.MapGet("/api/assets", async (IAssetRepository assetRepository) => Results.Ok(await assetRepository.GetAssetsAsync()));

app.MapPost("/api/assets", async (AssetInput input) =>
{
    var validation = ValidateAsset(input);
    if (validation is not null)
        return Results.BadRequest(new { error = validation });

    var ticker = input.Ticker.Trim().ToUpperInvariant();
    var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    try
    {
        await using var connection = await Database.OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO Assets (Ticker, Name, Quantity, TargetPercent, Rating, Notes, CurrentPrice, PriceUpdatedAt, LastQuoteAttemptAt, CreatedAt)
                                VALUES ($ticker, $name, $quantity, $targetPercent, $rating, $notes, 0, NULL, NULL, $createdAt);";
        command.Parameters.AddWithValue("$ticker", ticker);
        command.Parameters.AddWithValue("$name", input.Name?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("$quantity", input.Quantity);
        command.Parameters.AddWithValue("$targetPercent", input.TargetPercent);
        command.Parameters.AddWithValue("$rating", (object?)input.Rating ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", input.Notes?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("$createdAt", now);
        await command.ExecuteNonQueryAsync();
        var asset = await Database.GetAssetByTickerAsync(connectionString, ticker);
        return Results.Created($"/api/assets/{asset!.Id}", asset);
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
    {
        return Results.Conflict(new { error = $"O ativo {ticker} já está cadastrado." });
    }
});

app.MapPut("/api/assets/{id:int}", async (int id, AssetInput input) =>
{
    var validation = ValidateAsset(input);
    if (validation is not null)
        return Results.BadRequest(new { error = validation });

    var existing = await Database.GetAssetByIdAsync(connectionString, id);
    if (existing is null)
        return Results.NotFound(new { error = "Ativo não encontrado." });

    var ticker = input.Ticker.Trim().ToUpperInvariant();
    var tickerChanged = !string.Equals(ticker, existing.Ticker, StringComparison.OrdinalIgnoreCase);
    try
    {
        await using var connection = await Database.OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE Assets
                                SET Ticker = $ticker, Name = $name, Quantity = $quantity, TargetPercent = $targetPercent,
                                    Rating = $rating, Notes = $notes,
                                    CurrentPrice = $currentPrice, PriceUpdatedAt = $priceUpdatedAt, LastQuoteAttemptAt = $lastQuoteAttemptAt
                                WHERE Id = $id;";
        command.Parameters.AddWithValue("$ticker", ticker);
        command.Parameters.AddWithValue("$name", input.Name?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("$quantity", input.Quantity);
        command.Parameters.AddWithValue("$targetPercent", input.TargetPercent);
        command.Parameters.AddWithValue("$rating", (object?)input.Rating ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", input.Notes?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("$currentPrice", tickerChanged ? 0 : existing.CurrentPrice);
        command.Parameters.AddWithValue("$priceUpdatedAt", tickerChanged ? DBNull.Value : (object?)existing.PriceUpdatedAt?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastQuoteAttemptAt", tickerChanged ? DBNull.Value : (object?)existing.LastQuoteAttemptAt?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
        return Results.Ok(await Database.GetAssetByIdAsync(connectionString, id));
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
    {
        return Results.Conflict(new { error = $"O ativo {ticker} já está cadastrado." });
    }
});

app.MapDelete("/api/assets/{id:int}", async (int id) =>
{
    var deleted = await Database.DeleteAssetAsync(connectionString, id);
    return deleted ? Results.NoContent() : Results.NotFound(new { error = "Ativo não encontrado." });
});

app.MapPost("/api/quotes/refresh", async (
    IQuoteRefreshService quoteRefreshService,
    CancellationToken cancellationToken,
    [FromQuery] int pageNumber = PaginationLimits.DefaultPageNumber,
    [FromQuery] int pageSize = PaginationLimits.DefaultPageSize,
    [FromQuery] string? search = null) =>
{
    if (pageNumber < 1)
        return Results.BadRequest(new { error = "O número da página deve ser maior que zero." });
    if (pageSize is < 1 or > PaginationLimits.MaximumPageSize)
        return Results.BadRequest(new { error = $"O tamanho da página deve estar entre 1 e {PaginationLimits.MaximumPageSize}." });
    if (search?.Length > 100)
        return Results.BadRequest(new { error = "A busca deve ter até 100 caracteres." });

    var result = await quoteRefreshService.RefreshVisibleQuotesAsync(pageNumber, pageSize, search, cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/quotes/{id:int}/refresh", async (
    int id,
    IQuoteRefreshService quoteRefreshService,
    CancellationToken cancellationToken) =>
{
    var result = await quoteRefreshService.RefreshQuoteAsync(id, cancellationToken);
    return result is null
        ? Results.NotFound(new { error = "Ativo não encontrado." })
        : Results.Ok(result);
});

app.Run();

static string? ValidateAsset(AssetInput input)
{
    if (string.IsNullOrWhiteSpace(input.Ticker) || !Regex.IsMatch(input.Ticker.Trim(), "^[A-Za-z0-9.\\-]{1,15}$"))
        return "Informe um ticker válido, com até 15 letras/números.";
    if (input.Quantity < 0)
        return "A quantidade não pode ser negativa.";
    if (input.TargetPercent < 0 || input.TargetPercent > 100)
        return "A carteira ideal deve estar entre 0% e 100%.";
    if (input.Rating is not null and (< 1 or > 10))
        return "A nota deve estar entre 1 e 10.";
    return null;
}

public sealed record AssetInput(string Ticker, string? Name, int Quantity, decimal TargetPercent, int? Rating, string? Notes);
public sealed record SettingsInput(decimal Patrimony, string? ApiToken, bool ClearApiToken = false);

public static class Database
{
    public static async Task<SqliteConnection> OpenAsync(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public static async Task InitializeAsync(string connectionString)
    {
        await using var connection = await OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS Assets (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Ticker TEXT NOT NULL UNIQUE COLLATE NOCASE,
    Name TEXT NOT NULL DEFAULT '',
    Quantity INTEGER NOT NULL DEFAULT 0 CHECK (Quantity >= 0),
    TargetPercent REAL NOT NULL DEFAULT 0 CHECK (TargetPercent >= 0 AND TargetPercent <= 100),
    Rating INTEGER NULL CHECK (Rating IS NULL OR (Rating >= 1 AND Rating <= 10)),
    Notes TEXT NOT NULL DEFAULT '',
    CurrentPrice REAL NOT NULL DEFAULT 0 CHECK (CurrentPrice >= 0),
    PriceUpdatedAt TEXT NULL,
    LastQuoteAttemptAt TEXT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Settings (
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    Patrimony REAL NOT NULL DEFAULT 0,
    ApiToken TEXT NULL
);
INSERT OR IGNORE INTO Settings (Id, Patrimony, ApiToken) VALUES (1, 0, NULL);";
        await command.ExecuteNonQueryAsync();

        await EnsureLastQuoteAttemptColumnAsync(connection);
    }

    public static async Task SaveSettingsAsync(string connectionString, decimal patrimony, string? apiToken)
    {
        await using var connection = await OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Settings SET Patrimony = $patrimony, ApiToken = $apiToken WHERE Id = 1;";
        command.Parameters.AddWithValue("$patrimony", patrimony);
        command.Parameters.AddWithValue("$apiToken", (object?)apiToken ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<Asset?> GetAssetByIdAsync(string connectionString, int id)
    {
        await using var connection = await OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Ticker, Name, Quantity, TargetPercent, Rating, Notes, CurrentPrice, PriceUpdatedAt, LastQuoteAttemptAt, CreatedAt FROM Assets WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadAsset(reader) : null;
    }

    public static async Task<Asset?> GetAssetByTickerAsync(string connectionString, string ticker)
    {
        await using var connection = await OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Ticker, Name, Quantity, TargetPercent, Rating, Notes, CurrentPrice, PriceUpdatedAt, LastQuoteAttemptAt, CreatedAt FROM Assets WHERE Ticker = $ticker COLLATE NOCASE;";
        command.Parameters.AddWithValue("$ticker", ticker);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadAsset(reader) : null;
    }

    public static async Task<bool> DeleteAssetAsync(string connectionString, int id)
    {
        await using var connection = await OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Assets WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static Asset ReadAsset(SqliteDataReader reader)
    {
        var updatedAt = reader.IsDBNull(8) ? (DateTimeOffset?)null : DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var lastQuoteAttemptAt = reader.IsDBNull(9) ? (DateTimeOffset?)null : DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var createdAt = DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new Asset(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            Convert.ToDecimal(reader.GetDouble(4), CultureInfo.InvariantCulture),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.GetString(6),
            Convert.ToDecimal(reader.GetDouble(7), CultureInfo.InvariantCulture),
            updatedAt,
            lastQuoteAttemptAt,
            createdAt);
    }

    private static async Task EnsureLastQuoteAttemptColumnAsync(SqliteConnection connection)
    {
        var columnExists = false;
        await using (var infoCommand = connection.CreateCommand())
        {
            infoCommand.CommandText = "PRAGMA table_info(Assets);";
            await using var reader = await infoCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), "LastQuoteAttemptAt", StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        if (columnExists) return;

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = "ALTER TABLE Assets ADD COLUMN LastQuoteAttemptAt TEXT NULL;";
        await alterCommand.ExecuteNonQueryAsync();
    }
}
