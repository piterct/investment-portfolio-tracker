namespace InvestmentPortfolioTracker.Business.Models
{
    public class Asset : Entity
    {
        public Asset(int id, string ticker, string name, int quantity, decimal targetPercent, int? rating, string notes, decimal currentPrice, DateTimeOffset? priceUpdatedAt, DateTimeOffset createdAt)
        {
            Id = id;
            Ticker = ticker;
            Name = name;
            Quantity = quantity;
            TargetPercent = targetPercent;
            Rating = rating;
            Notes = notes;
            CurrentPrice = currentPrice;
            PriceUpdatedAt = priceUpdatedAt;
            CreatedAt = createdAt;
        }
        public string Ticker { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public decimal TargetPercent { get; set; }
        public int? Rating { get; set; }
        public string Notes { get; set; }
        public decimal CurrentPrice { get; set; }
        public DateTimeOffset? PriceUpdatedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public decimal GetMarketValue()
        {
            return CurrentPrice * Quantity;
        }
    }
}
