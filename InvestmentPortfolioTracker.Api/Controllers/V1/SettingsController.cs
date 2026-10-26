using InvestmentPortfolioTracker.Business.Models;
using InvestmentPortfolioTracker.Business.Notifications;
using InvestmentPortfolioTracker.Business.Repositories;
using InvestmentPortfolioTracker.Repository.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentPortfolioTracker.Api.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class SettingsController : MainController
    {
        private readonly ISettingsRepository _settingsRepository;

        public SettingsController(INotifier notifier, ISettingsRepository settingsRepository) : base(notifier)
        {
            _settingsRepository = settingsRepository;
        }
      

        [HttpGet]
        [Route("v1/settings")]
        public async Task<AppSettings> GetSettingsAsync()
        {
            return await _settingsRepository.GetSettingsAsync();
        }
    }
}
