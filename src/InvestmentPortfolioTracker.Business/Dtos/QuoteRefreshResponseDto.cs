namespace InvestmentPortfolioTracker.Business.Dtos
{
    public sealed class QuoteRefreshResponseDto
    {
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
        public int Requested { get; init; }
        public int Updated { get; init; }
        public int Skipped { get; init; }
        public IReadOnlyCollection<QuoteRefreshFailureDto> Failed { get; init; } = Array.Empty<QuoteRefreshFailureDto>();
        public DateTimeOffset RefreshedAt { get; init; }
    }
}
