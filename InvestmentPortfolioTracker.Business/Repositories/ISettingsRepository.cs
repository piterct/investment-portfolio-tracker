using InvestmentPortfolioTracker.Business.Models;

namespace InvestmentPortfolioTracker.Business.Repositories
{
    public interface ISettingsRepository
    {
        Task<AppSettings> GetSettingsAsync();
    }
}
