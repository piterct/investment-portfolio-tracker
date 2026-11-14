using InvestmentPortfolioTracker.Business.Dtos;
using InvestmentPortfolioTracker.Business.Models;
using InvestmentPortfolioTracker.Business.Notifications;
using InvestmentPortfolioTracker.Business.Repositories;

namespace InvestmentPortfolioTracker.Business.Services
{
    public class PortfolioService(INotifier notifier, ISettingsRepository settingsRepository, IAssetRepository assetRepository)
        : BaseService(notifier), IPortfolioService
    {
        public async Task<PortfolioResponseDto> GetPortFolioAsync(
            int pageNumber = PaginationLimits.DefaultPageNumber,
            int pageSize = PaginationLimits.DefaultPageSize,
            string? searchTerm = null)
        {
            var settings = await settingsRepository.GetSettingsAsync();
            var assetPage = await assetRepository.GetPortfolioPageAsync(pageNumber, pageSize, searchTerm);
            var totalMarketValue = assetPage.TotalMarketValue;
            var referencePatrimony = settings.GetReferencePatrimony(totalMarketValue);

            var assetsList = CreateAssetsDto(assetPage.Assets, referencePatrimony);

            var investedPercent = referencePatrimony > 0
                ? totalMarketValue / referencePatrimony * 100m
                : 0m;

            return CreatePortfolioResponseDto(
                referencePatrimony,
                settings.Patrimony,
                totalMarketValue,
                assetPage.TotalAssetCount,
                assetPage.TotalTargetPercent,
                investedPercent,
                assetPage.LatestPriceUpdatedAt,
                new PaginationMetadataDto
                {
                    PageNumber = assetPage.PageNumber,
                    PageSize = assetPage.PageSize,
                    TotalItems = assetPage.TotalItems,
                    TotalPages = assetPage.TotalPages
                },
                assetsList);
        }

        private static List<PortfolioAssetAllocationDto> CreateAssetsDto(List<Asset> assets, decimal referencePatrimony)
        {
            var portFolioAssetAllocationList = new List<PortfolioAssetAllocationDto>();

            foreach (var asset in assets)
            {
                var marketValue = asset.MarketValue();
                var targetValue = asset.TargetValue(referencePatrimony);
                var difference = targetValue - marketValue;
                var quantityToTarget = asset.CurrentPrice > 0
                    ? decimal.ToInt32(decimal.Truncate(difference / asset.CurrentPrice))
                    : 0;

                var portFolioAssetAllocation = new PortfolioAssetAllocationDto(asset.Id, asset.Ticker, asset.Name, asset.Quantity, asset.CurrentPrice, asset.PriceUpdatedAt, asset.TargetPercent, asset.Rating, asset.Notes)
                    {
                        MarketValue = marketValue,
                        TargetValue = targetValue,
                        CurrentPercent = referencePatrimony > 0
                            ? marketValue / referencePatrimony * 100m
                            : 0m,
                        Difference = difference,
                        QuantityToTarget = quantityToTarget,
                        Recommendation = quantityToTarget > 0 ? "Comprar" : "Aguardar"
                    };

                portFolioAssetAllocationList.Add(portFolioAssetAllocation);
            }

            return portFolioAssetAllocationList;
        }

        private static PortfolioResponseDto CreatePortfolioResponseDto(decimal patrimony, decimal configuredPatrimony, decimal totalMarketValue,
            int assetCount, decimal targetPercentTotal, decimal investedPercent, DateTimeOffset? latestPriceUpdatedAt,
            PaginationMetadataDto pagination,
            List<PortfolioAssetAllocationDto> assets)
        {
            var portfolioResponseDto = new PortfolioResponseDto
            {
                Patrimony = patrimony,
                ConfiguredPatrimony = configuredPatrimony,
                TotalMarketValue = totalMarketValue,
                AssetCount = assetCount,
                TargetPercentTotal = targetPercentTotal,
                InvestedPercent = investedPercent,
                LatestPriceUpdatedAt = latestPriceUpdatedAt,
                Pagination = pagination,
                Assets = assets
            };

            return portfolioResponseDto;
        }
    }
}
