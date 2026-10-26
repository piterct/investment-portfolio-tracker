using InvestmentPortfolioTracker.Business.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentPortfolioTracker.Api.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class PortfolioController(INotifier notifier) : MainController(notifier);

}
