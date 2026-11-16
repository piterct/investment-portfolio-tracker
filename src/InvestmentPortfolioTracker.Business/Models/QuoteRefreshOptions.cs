namespace InvestmentPortfolioTracker.Business.Models
{
    public sealed class QuoteRefreshOptions
    {
        public int FreshnessMinutes { get; init; } = 2;

        public TimeSpan FreshnessInterval => TimeSpan.FromMinutes(Math.Max(0, FreshnessMinutes));
    }
}
