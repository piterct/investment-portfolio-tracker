using InvestmentPortfolioTracker.Business.Notifications;

namespace InvestmentPortfolioTracker.Business.Interfaces
{
    public interface INotifier
    {
        bool HasNotification();
        List<Notification> GetNotifications();
        void Handle(Notification notification);
    }
}
