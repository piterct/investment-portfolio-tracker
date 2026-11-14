namespace InvestmentPortfolioTracker.Business.Notifications
{
    public class Notification(string message)
    {
        public string Message { get; set; } = message;
    }
}
