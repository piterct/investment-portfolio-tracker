namespace InvestmentPortfolioTracker.Business.Dtos
{
    public class PortfolioResponseDto
    {
        public decimal Patrimony { get; set; }
        public decimal ConfiguredPatrimony { get; set; }
        public decimal TotalMarketValue { get; set; }
        public int AssetCount { get; set; }
        public decimal TargetPercentTotal { get; set; }
        public decimal InvestedPercent { get; set; }
        public List<PortfolioAssetAllocationDto> Assets { get; set; } = null!;
    }
}
