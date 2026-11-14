using InvestmentPortfolioTracker.Business.Dtos;

using InvestmentPortfolioTracker.Business.Models;

namespace InvestmentPortfolioTracker.Business.Services
{
    public interface IPortfolioService
    {
        Task<PortfolioResponseDto> GetPortFolioAsync(
            int pageNumber = PaginationLimits.DefaultPageNumber,
            int pageSize = PaginationLimits.DefaultPageSize,
            string? searchTerm = null);
    }
}
