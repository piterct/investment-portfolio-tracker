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
            var totalMarketValue = assets.Sum(a => a.CurrentPrice * a.Quantity);
            var referencePatrimony = settings.Patrimony > 0 ? settings.Patrimony : totalMarketValue;
            CreateAssetsDto(assets);


            assets.Select(a =>
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
                    TargetValue = targetValue,
                    Difference = difference,
                    CurrentPercent = referencePatrimony > 0 ? marketValue / referencePatrimony * 100m : 0m,
                    QuantityToTarget = quantityToTarget,
                    Recommendation = quantityToTarget > 0 ? "Comprar" : "Aguardar"
                };
            }).ToList();

            return Task.FromResult(new object());
        }

        private static List<PortfolioAssetAllocationDto> CreateAssetsDto(List<Asset> assets)
        {
            var portFolioAssetAllocationList = new List<PortfolioAssetAllocationDto>();

            foreach (var asset in assets)
            {
                var portFolioAssetAllocation = new PortfolioAssetAllocationDto(asset.Id, asset.Ticker, asset.Name, asset.Quantity, asset.CurrentPrice, asset.PriceUpdatedAt, asset.TargetPercent, asset.Rating, asset.Notes);

                portFolioAssetAllocation.MarketValue = asset.GetMarketValue();
                portFolioAssetAllocation.TargetPercent = asset.GetTargetValue();

                portFolioAssetAllocationList.Add(portFolioAssetAllocation);


            }

            return portFolioAssetAllocationList;
        }

        private 
    }
}
