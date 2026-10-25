using InvestmentPortfolioTracker.Business.Models;
using InvestmentPortfolioTracker.Business.Repositories;
using InvestmentPortfolioTracker.Repository.DataContext;
using System.Globalization;

namespace InvestmentPortfolioTracker.Repository.Repositories
{
    public class SettingsRepository(InvestmentPortfolioContext context) : ISettingsRepository
    {
        public async Task<AppSettings> GetSettingsAsync()
        {
            await using var connection = context.CreateConnection();

            await connection.OpenAsync();

            await using var command = connection.CreateCommand();

            command.CommandText = "SELECT Patrimony, ApiToken FROM Settings WHERE Id = 1;";

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync()) return new AppSettings(0, 0, null);

            return new AppSettings(0, Convert.ToDecimal(reader.GetDouble(0), CultureInfo.InvariantCulture),
                reader.IsDBNull(1) ? null : reader.GetString(1));
        }

    }
}
