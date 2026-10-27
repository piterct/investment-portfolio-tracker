using InvestmentPortfolioTracker.Business.Models;
using InvestmentPortfolioTracker.Repository.DataContext;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace InvestmentPortfolioTracker.Repository.Repositories
{
    public class AssetRepository(InvestmentPortfolioContext context)
    {
        public async Task<List<Asset>> GetAssetsAsync()
        {
            var list = new List<Asset>();

            await using var connection = context.CreateConnection();

            await connection.OpenAsync();

            await using var command = connection.CreateCommand();

            command.CommandText = "SELECT Id, Ticker, Name, Quantity, TargetPercent, Rating, Notes, CurrentPrice, PriceUpdatedAt, CreatedAt FROM Assets ORDER BY Ticker;";

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync()) list.Add(ReadAsset(reader));
            return list;
        }

        private static Asset ReadAsset(SqliteDataReader reader)
        {
            var updatedAt = reader.IsDBNull(8) ? (DateTimeOffset?)null : DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var createdAt = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
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
                createdAt);
        }
    }
}
