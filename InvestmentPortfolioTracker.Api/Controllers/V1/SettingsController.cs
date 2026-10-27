using InvestmentPortfolioTracker.Business.Models;
using InvestmentPortfolioTracker.Business.Notifications;
using InvestmentPortfolioTracker.Business.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentPortfolioTracker.Api.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class SettingsController(INotifier notifier, ISettingsRepository settingsRepository)
        : MainController(notifier)
    {
        [HttpGet]
        [Route("v1/settings")]
        public async Task<AppSettings> GetSettingsAsync()
        {
            return await settingsRepository.GetSettingsAsync();
        }
    }
}
