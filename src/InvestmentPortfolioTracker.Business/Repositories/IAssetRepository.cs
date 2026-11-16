using InvestmentPortfolioTracker.Business.Models;

namespace InvestmentPortfolioTracker.Business.Repositories
{
    public interface IAssetRepository
    {
        Task<List<Asset>> GetAssetsAsync();
        Task<Asset?> GetAssetByIdAsync(int id);
        Task<PortfolioAssetPage> GetPortfolioPageAsync(int pageNumber, int pageSize, string? searchTerm = null);
        Task UpdateQuoteAsync(int id, decimal price, DateTimeOffset updatedAt);
        Task RecordQuoteAttemptAsync(int id, DateTimeOffset attemptedAt);
    }
}
