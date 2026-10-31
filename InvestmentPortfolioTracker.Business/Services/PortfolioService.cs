using InvestmentPortfolioTracker.Business.Notifications;
using InvestmentPortfolioTracker.Business.Repositories;

namespace InvestmentPortfolioTracker.Business.Services
{
    public class PortfolioService(INotifier notifier, ISettingsRepository settingsRepository, IAssetRepository assetRepository)
        : BaseService(notifier), IPortfolioService
    {
        public async Task<object> GetPortFolioAsync()
        {
            var settings = await settingsRepository.GetSettingsAsync();
            var assets = await assetRepository.GetAssetsAsync();
            var totalMarketValue = assets.Sum(a => a.CurrentPrice * a.Quantity);

            return Task.FromResult(new object());
        }
    }
}
