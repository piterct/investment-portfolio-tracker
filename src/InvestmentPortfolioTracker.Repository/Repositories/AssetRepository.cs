using InvestmentPortfolioTracker.Business.Models;
using InvestmentPortfolioTracker.Business.Repositories;
using InvestmentPortfolioTracker.Repository.DataContext;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace InvestmentPortfolioTracker.Repository.Repositories
{
    public class AssetRepository(InvestmentPortfolioContext context) : IAssetRepository
    {
        public async Task<List<Asset>> GetAssetsAsync()
        {
            var list = new List<Asset>();

            await using var connection = context.CreateConnection();

            await connection.OpenAsync();

            await using var command = connection.CreateCommand();

            command.CommandText = "SELECT Id, Ticker, Name, Quantity, TargetPercent, Rating, Notes, CurrentPrice, PriceUpdatedAt, LastQuoteAttemptAt, CreatedAt FROM Assets ORDER BY Ticker;";

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync()) list.Add(ReadAsset(reader));

            return list;
        }

        public async Task<Asset?> GetAssetByIdAsync(int id)
        {
            await using var connection = context.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Ticker, Name, Quantity, TargetPercent, Rating, Notes, CurrentPrice, PriceUpdatedAt, LastQuoteAttemptAt, CreatedAt FROM Assets WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", id);
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? ReadAsset(reader) : null;
        }

        public async Task UpdateQuoteAsync(int id, decimal price, DateTimeOffset updatedAt)
        {
            await using var connection = context.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Assets SET CurrentPrice = $price, PriceUpdatedAt = $updatedAt, LastQuoteAttemptAt = $updatedAt WHERE Id = $id;";
            command.Parameters.AddWithValue("$price", price);
            command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task RecordQuoteAttemptAsync(int id, DateTimeOffset attemptedAt)
        {
            await using var connection = context.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Assets SET LastQuoteAttemptAt = $attemptedAt WHERE Id = $id;";
            command.Parameters.AddWithValue("$attemptedAt", attemptedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<PortfolioAssetPage> GetPortfolioPageAsync(int pageNumber, int pageSize, string? searchTerm = null)
        {
            if (pageNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "O número da página deve ser maior que zero.");
            if (pageSize is < 1 or > PaginationLimits.MaximumPageSize)
                throw new ArgumentOutOfRangeException(nameof(pageSize), $"O tamanho da página deve estar entre 1 e {PaginationLimits.MaximumPageSize}.");
            if (searchTerm?.Length > 100)
                throw new ArgumentOutOfRangeException(nameof(searchTerm), "A busca deve ter até 100 caracteres.");

            var assets = new List<Asset>();
            var offset = ((long)pageNumber - 1) * pageSize;
            var searchPattern = string.IsNullOrWhiteSpace(searchTerm)
                ? null
                : $"%{EscapeLikePattern(searchTerm.Trim())}%";

            await using var connection = context.CreateConnection();
            await connection.OpenAsync();

            int totalItems;
            int matchingItems;
            decimal totalMarketValue;
            decimal totalTargetPercent;
            DateTimeOffset? latestPriceUpdatedAt;

            await using (var summaryCommand = connection.CreateCommand())
            {
                summaryCommand.CommandText = @"SELECT COUNT(*),
                                                      COALESCE(SUM(CurrentPrice * Quantity), 0),
                                                      COALESCE(SUM(TargetPercent), 0),
                                                      MAX(PriceUpdatedAt),
                                                      (SELECT COUNT(*) FROM Assets
                                                       WHERE $searchPattern IS NULL
                                                          OR Ticker LIKE $searchPattern ESCAPE '\'
                                                          OR Name LIKE $searchPattern ESCAPE '\'
                                                          OR Notes LIKE $searchPattern ESCAPE '\')
                                               FROM Assets;";
                summaryCommand.Parameters.AddWithValue("$searchPattern", (object?)searchPattern ?? DBNull.Value);

                await using var summaryReader = await summaryCommand.ExecuteReaderAsync();
                await summaryReader.ReadAsync();
                totalItems = checked((int)summaryReader.GetInt64(0));
                totalMarketValue = Convert.ToDecimal(summaryReader.GetDouble(1), CultureInfo.InvariantCulture);
                totalTargetPercent = Convert.ToDecimal(summaryReader.GetDouble(2), CultureInfo.InvariantCulture);
                latestPriceUpdatedAt = summaryReader.IsDBNull(3)
                    ? null
                    : DateTimeOffset.Parse(summaryReader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                matchingItems = checked((int)summaryReader.GetInt64(4));
            }

            await using (var pageCommand = connection.CreateCommand())
            {
                pageCommand.CommandText = @"SELECT Id, Ticker, Name, Quantity, TargetPercent, Rating, Notes, CurrentPrice, PriceUpdatedAt, LastQuoteAttemptAt, CreatedAt
                                           FROM Assets
                                           WHERE $searchPattern IS NULL
                                              OR Ticker LIKE $searchPattern ESCAPE '\'
                                              OR Name LIKE $searchPattern ESCAPE '\'
                                              OR Notes LIKE $searchPattern ESCAPE '\'
                                           ORDER BY Ticker
                                           LIMIT $pageSize OFFSET $offset;";
                pageCommand.Parameters.AddWithValue("$searchPattern", (object?)searchPattern ?? DBNull.Value);
                pageCommand.Parameters.AddWithValue("$pageSize", pageSize);
                pageCommand.Parameters.AddWithValue("$offset", offset);

                await using var reader = await pageCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync()) assets.Add(ReadAsset(reader));
            }

            return new PortfolioAssetPage
            {
                Assets = assets,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = matchingItems,
                TotalAssetCount = totalItems,
                TotalMarketValue = totalMarketValue,
                TotalTargetPercent = totalTargetPercent,
                LatestPriceUpdatedAt = latestPriceUpdatedAt
            };
        }

        private static string EscapeLikePattern(string value)
        {
            return value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
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
    }
}
