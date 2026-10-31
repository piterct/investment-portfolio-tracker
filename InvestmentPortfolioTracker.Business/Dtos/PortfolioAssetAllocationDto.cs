namespace InvestmentPortfolioTracker.Business.Dtos
{
    public class PortfolioAssetAllocationDto(
        int id,
        string ticker,
        string name,
        int quantity,
        decimal currentPrice,
        DateTimeOffset? priceUpdatedAt,
        decimal targetPercent,
        int? rating,
        string notes,
        decimal marketValue,
        decimal currentPercent,
        decimal difference,
        int quantityToTarget,
        string recommendation)
    {
        public int Id { get; set; } = id;
        public string Ticker { get; set; } = ticker;
        public string Name { get; set; } = name;
        public int Quantity { get; set; } = quantity;
        public decimal CurrentPrice { get; set; } = currentPrice;
        public DateTimeOffset? PriceUpdatedAt { get; set; } = priceUpdatedAt;
        public decimal TargetPercent { get; set; } = targetPercent;
        public int? Rating { get; set; } = rating;
        public string Notes { get; set; } = notes;
        public decimal MarketValue { get; set; } = marketValue;
        public decimal CurrentPercent { get; set; } = currentPercent;
        public decimal TargetValue { get; set; }
        public decimal Difference { get; set; } = difference;
        public int QuantityToTarget { get; set; } = quantityToTarget;
        public string Recommendation { get; set; } = recommendation;
    }
}
