using InvestmentPortfolioTracker.Business.Dtos;
using InvestmentPortfolioTracker.Business.Models;

namespace InvestmentPortfolioTracker.Business.Services
{
    public interface IQuoteRefreshService
    {
        Task<QuoteRefreshResponseDto> RefreshVisibleQuotesAsync(
            int pageNumber = PaginationLimits.DefaultPageNumber,
            int pageSize = PaginationLimits.DefaultPageSize,
            string? searchTerm = null,
            CancellationToken cancellationToken = default);

        Task<QuoteRefreshResponseDto?> RefreshQuoteAsync(
            int assetId,
            CancellationToken cancellationToken = default);
    }
}
