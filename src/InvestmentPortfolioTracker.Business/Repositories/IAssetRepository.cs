using InvestmentPortfolioTracker.Business.Models;

namespace InvestmentPortfolioTracker.Business.Repositories
{
    public interface IAssetRepository
    {
        Task<List<Asset>> GetAssetsAsync();
    }
}
