using InvestmentPortfolioTracker.Business.Dtos;
using InvestmentPortfolioTracker.Business.Models;
using InvestmentPortfolioTracker.Business.Notifications;
using InvestmentPortfolioTracker.Business.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace InvestmentPortfolioTracker.Api.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class PortfolioController(INotifier notifier, IPortfolioService portfolioService) : MainController(notifier)
    {
        [HttpGet]
        [ProducesResponseType(typeof(PortfolioResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<PortfolioResponseDto>> GetPortfolioAsync(
            [FromQuery, Range(1, int.MaxValue)] int pageNumber = PaginationLimits.DefaultPageNumber,
            [FromQuery, Range(1, PaginationLimits.MaximumPageSize)] int pageSize = PaginationLimits.DefaultPageSize,
            [FromQuery, StringLength(100)] string? search = null)
        {
            var portfolio = await portfolioService.GetPortFolioAsync(pageNumber, pageSize, search);
            return Ok(portfolio);
        }
    }
}
