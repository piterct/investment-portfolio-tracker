namespace InvestmentPortfolioTracker.Business.Dtos
{
    public sealed class QuoteRefreshFailureDto(string ticker, string error)
    {
        public string Ticker { get; } = ticker;
        public string Error { get; } = error;
    }
}
