namespace InvestmentPortfolioTracker.Business.Dtos
{
    public sealed class PaginationMetadataDto
    {
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
        public int TotalItems { get; init; }
        public int TotalPages { get; init; }
        public bool HasPreviousPage => PageNumber > 1 && TotalPages > 0;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
