namespace InvestmentPortfolioTracker.Business.Models
{
    public sealed class PortfolioAssetPage
    {
        public required List<Asset> Assets { get; init; }
        public required int PageNumber { get; init; }
        public required int PageSize { get; init; }
        public required int TotalItems { get; init; }
        public required int TotalAssetCount { get; init; }
        public required decimal TotalMarketValue { get; init; }
        public required decimal TotalTargetPercent { get; init; }
        public DateTimeOffset? LatestPriceUpdatedAt { get; init; }

        public int TotalPages => TotalItems == 0
            ? 0
            : (int)Math.Ceiling((decimal)TotalItems / PageSize);
    }
}
