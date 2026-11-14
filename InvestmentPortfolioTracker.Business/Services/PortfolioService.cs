using InvestmentPortfolioTracker.Business.Dtos;
using InvestmentPortfolioTracker.Business.Models;
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
            var totalMarketValue = SumTotalMarketValue(assets);
            var referencePatrimony = settings.GetReferencePatrimony(totalMarketValue);

           var assetsList = CreateAssetsDto(assets, referencePatrimony);

           return Task.FromResult(CreatePortfolioResponseDto(referencePatrimony, settings.Patrimony, totalMarketValue, assets.Count,
               assets.Sum(a => a.TargetPercent), referencePatrimony, assetsList));
        }

        private static List<PortfolioAssetAllocationDto> CreateAssetsDto(List<Asset> assets, decimal referencePatrimony)
        {
            var portFolioAssetAllocationList = new List<PortfolioAssetAllocationDto>();

            foreach (var asset in assets)
            {
                var portFolioAssetAllocation = new PortfolioAssetAllocationDto(asset.Id, asset.Ticker, asset.Name, asset.Quantity, asset.CurrentPrice, asset.PriceUpdatedAt, asset.TargetPercent, asset.Rating, asset.Notes)
                    {
                        MarketValue = asset.MarketValue(),
                        TargetPercent = asset.TargetValue(referencePatrimony),
                        Difference = asset.Difference(referencePatrimony),
                        QuantityToTarget = asset.QuantityToTarget(referencePatrimony),
                        Recommendation = asset.Recommendation(referencePatrimony)
                    };

                portFolioAssetAllocationList.Add(portFolioAssetAllocation);
            }

            return portFolioAssetAllocationList;
        }

        private static PortfolioResponseDto CreatePortfolioResponseDto(decimal patrimony, decimal configuredPatrimony, decimal totalMarketValue,
            int assetCount, decimal targetPercentTotal, decimal investedPercent, List<PortfolioAssetAllocationDto> assets)
        {
            var portfolioResponseDto = new PortfolioResponseDto
            {
                Patrimony = patrimony,
                ConfiguredPatrimony = configuredPatrimony,
                TotalMarketValue = totalMarketValue,
                AssetCount = assetCount,
                TargetPercentTotal = targetPercentTotal,
                InvestedPercent = investedPercent,
                Assets = assets
            };

            return portfolioResponseDto;
        }

        private static decimal SumTotalMarketValue(List<Asset> assetList)
        {
            return assetList.Sum(a => a.CurrentPrice * a.Quantity);
        }

    }
}
