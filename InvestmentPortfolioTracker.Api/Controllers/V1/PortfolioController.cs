using InvestmentPortfolioTracker.Business.Dtos;
using InvestmentPortfolioTracker.Business.Notifications;
using InvestmentPortfolioTracker.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentPortfolioTracker.Api.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class PortfolioController(INotifier notifier, IPortfolioService portfolioService) : MainController(notifier)
    {
        [HttpGet]
        [ProducesResponseType(typeof(PortfolioResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<PortfolioResponseDto>> GetPortfolioAsync()
        {
            var portfolio = await portfolioService.GetPortFolioAsync();
            return Ok(portfolio);
        }
    }
}
