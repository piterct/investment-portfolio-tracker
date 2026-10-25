using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("brapi", client =>
{
    client.BaseAddress = new Uri("https://brapi.dev/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("InvestmentPortfolioTracker/1.0");
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

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", database = Path.GetFileName(databasePath) }));

app.MapGet("/api/settings", async () =>
{
    var settings = await Database.GetSettingsAsync(connectionString);
    return Results.Ok(new { patrimony = settings.Patrimony, hasApiToken = !string.IsNullOrWhiteSpace(settings.ApiToken) });
});

app.MapPut("/api/settings", async (SettingsInput input) =>
{
    if (input.Patrimony < 0)
        return Results.BadRequest(new { error = "O patrimônio de referência não pode ser negativo." });

    var existing = await Database.GetSettingsAsync(connectionString);
    var token = input.ClearApiToken
        ? null
        : string.IsNullOrWhiteSpace(input.ApiToken)
            ? existing.ApiToken
            : input.ApiToken.Trim();

    await Database.SaveSettingsAsync(connectionString, input.Patrimony, token);
    return Results.Ok(new { patrimony = input.Patrimony, hasApiToken = !string.IsNullOrWhiteSpace(token) });
});

app.MapGet("/api/assets", async () => Results.Ok(await Database.GetAssetsAsync(connectionString)));

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
        command.CommandText = @"INSERT INTO Assets (Ticker, Name, Quantity, TargetPercent, Rating, Notes, CurrentPrice, PriceUpdatedAt, CreatedAt)
                                VALUES ($ticker, $name, $quantity, $targetPercent, $rating, $notes, 0, NULL, $createdAt);";
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
                                    CurrentPrice = $currentPrice, PriceUpdatedAt = $priceUpdatedAt
                                WHERE Id = $id;";
        command.Parameters.AddWithValue("$ticker", ticker);
        command.Parameters.AddWithValue("$name", input.Name?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("$quantity", input.Quantity);
        command.Parameters.AddWithValue("$targetPercent", input.TargetPercent);
        command.Parameters.AddWithValue("$rating", (object?)input.Rating ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", input.Notes?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("$currentPrice", tickerChanged ? 0 : existing.CurrentPrice);
        command.Parameters.AddWithValue("$priceUpdatedAt", tickerChanged ? DBNull.Value : (object?)existing.PriceUpdatedAt?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
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

app.MapPost("/api/quotes/refresh", async (IHttpClientFactory httpClientFactory) =>
{
    var assets = await Database.GetAssetsAsync(connectionString);
    if (assets.Count == 0)
        return Results.Ok(new { updated = 0, failed = Array.Empty<object>(), refreshedAt = DateTimeOffset.UtcNow });

    var settings = await Database.GetSettingsAsync(connectionString);
    var client = httpClientFactory.CreateClient("brapi");
    var updated = 0;
    var failures = new List<object>();
    var semaphore = new SemaphoreSlim(4, 4);

    var tasks = assets.Select(async asset =>
    {
        await semaphore.WaitAsync();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/quote/{Uri.EscapeDataString(asset.Ticker)}");
            if (!string.IsNullOrWhiteSpace(settings.ApiToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiToken);

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                lock (failures)
                    failures.Add(new { ticker = asset.Ticker, error = response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden
                        ? "A API recusou a consulta. Confira seu token/plano da brapi."
                        : $"A API respondeu {(int)response.StatusCode}. Confira o token, o ticker e os limites do plano." });
                return;
            }

            using var json = JsonDocument.Parse(body);
            if (!json.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
            {
                lock (failures) failures.Add(new { ticker = asset.Ticker, error = "A API não retornou cotação para este ticker." });
                return;
            }

            var first = results[0];
            if (!first.TryGetProperty("regularMarketPrice", out var priceElement) || !priceElement.TryGetDecimal(out var price) || price < 0)
            {
                lock (failures) failures.Add(new { ticker = asset.Ticker, error = "A cotação retornada não contém um preço válido." });
                return;
            }

            await Database.UpdateQuoteAsync(connectionString, asset.Id, price, DateTimeOffset.UtcNow);
            Interlocked.Increment(ref updated);
        }
        catch (TaskCanceledException)
        {
            lock (failures) failures.Add(new { ticker = asset.Ticker, error = "Tempo limite ao consultar a cotação." });
        }
        catch (JsonException)
        {
            lock (failures) failures.Add(new { ticker = asset.Ticker, error = "A resposta da API veio em formato inesperado." });
        }
        catch (HttpRequestException)
        {
            lock (failures) failures.Add(new { ticker = asset.Ticker, error = "Não foi possível conectar à API de cotações." });
        }
        finally
        {
            semaphore.Release();
        }
    });

    await Task.WhenAll(tasks);
    return Results.Ok(new { updated, failed = failures, refreshedAt = DateTimeOffset.UtcNow });
});

app.MapGet("/api/portfolio", async () =>
{
    var settings = await Database.GetSettingsAsync(connectionString);
    var assets = await Database.GetAssetsAsync(connectionString);
    var totalMarketValue = assets.Sum(a => a.CurrentPrice * a.Quantity);
    var referencePatrimony = settings.Patrimony > 0 ? settings.Patrimony : totalMarketValue;

    var calculatedAssets = assets.Select(a =>
    {
        var marketValue = a.CurrentPrice * a.Quantity;
        var targetValue = referencePatrimony * a.TargetPercent / 100m;
        var difference = targetValue - marketValue;
        var quantityToTarget = a.CurrentPrice > 0 ? decimal.ToInt32(decimal.Truncate(difference / a.CurrentPrice)) : 0;
        return new
        {
            a.Id,
            a.Ticker,
            a.Name,
            a.Quantity,
            a.CurrentPrice,
            a.PriceUpdatedAt,
            a.TargetPercent,
            a.Rating,
            a.Notes,
            MarketValue = marketValue,
            CurrentPercent = referencePatrimony > 0 ? marketValue / referencePatrimony * 100m : 0m,
            TargetValue = targetValue,
            Difference = difference,
            QuantityToTarget = quantityToTarget,
            Recommendation = quantityToTarget > 0 ? "Comprar" : "Aguardar"
        };
    }).ToList();

    return Results.Ok(new
    {
        patrimony = referencePatrimony,
        configuredPatrimony = settings.Patrimony,
        totalMarketValue,
        assetCount = assets.Count,
        targetPercentTotal = assets.Sum(a => a.TargetPercent),
        investedPercent = referencePatrimony > 0 ? totalMarketValue / referencePatrimony * 100m : 0m,
        assets = calculatedAssets
    });
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
public sealed record AppSettings(decimal Patrimony, string? ApiToken);
public sealed record AssetRecord(int Id, string Ticker, string Name, int Quantity, decimal TargetPercent, int? Rating, string Notes, decimal CurrentPrice, DateTimeOffset? PriceUpdatedAt, DateTimeOffset CreatedAt);

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
    CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Settings (
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    Patrimony REAL NOT NULL DEFAULT 0,
    ApiToken TEXT NULL
);
INSERT OR IGNORE INTO Settings (Id, Patrimony, ApiToken) VALUES (1, 0, NULL);";
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<AppSettings> GetSettingsAsync(string connectionString)
    {
        await using var connection = await OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Patrimony, ApiToken FROM Settings WHERE Id = 1;";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return new AppSettings(0, null);
        return new AppSettings(Convert.ToDecimal(reader.GetDouble(0), CultureInfo.InvariantCulture), reader.IsDBNull(1) ? null : reader.GetString(1));
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

    public static async Task<List<AssetRecord>> GetAssetsAsync(string connectionString)
    {
        var list = new List<AssetRecord>();
        await using var connection = await OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Ticker, Name, Quantity, TargetPercent, Rating, Notes, CurrentPrice, PriceUpdatedAt, CreatedAt FROM Assets ORDER BY Ticker;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(ReadAsset(reader));
        return list;
    }

    public static async Task<AssetRecord?> GetAssetByIdAsync(string connectionString, int id)
    {
        await using var connection = await OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Ticker, Name, Quantity, TargetPercent, Rating, Notes, CurrentPrice, PriceUpdatedAt, CreatedAt FROM Assets WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadAsset(reader) : null;
    }

    public static async Task<AssetRecord?> GetAssetByTickerAsync(string connectionString, string ticker)
    {
        await using var connection = await OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Ticker, Name, Quantity, TargetPercent, Rating, Notes, CurrentPrice, PriceUpdatedAt, CreatedAt FROM Assets WHERE Ticker = $ticker COLLATE NOCASE;";
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

    public static async Task UpdateQuoteAsync(string connectionString, int id, decimal price, DateTimeOffset updatedAt)
    {
        await using var connection = await OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Assets SET CurrentPrice = $price, PriceUpdatedAt = $updatedAt WHERE Id = $id;";
        command.Parameters.AddWithValue("$price", price);
        command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static AssetRecord ReadAsset(SqliteDataReader reader)
    {
        var updatedAt = reader.IsDBNull(8) ? (DateTimeOffset?)null : DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var createdAt = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new AssetRecord(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            Convert.ToDecimal(reader.GetDouble(4), CultureInfo.InvariantCulture),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.GetString(6),
            Convert.ToDecimal(reader.GetDouble(7), CultureInfo.InvariantCulture),
            updatedAt,
            createdAt);
    }
}
