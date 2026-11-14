using InvestmentPortfolioTracker.Business.Dtos;

namespace InvestmentPortfolioTracker.Business.Services
{
    public interface IPortfolioService
    {
        Task<PortfolioResponseDto> GetPortFolioAsync();
    }
}
