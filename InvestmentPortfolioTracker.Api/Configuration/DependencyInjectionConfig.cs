using InvestmentPortfolioTracker.Business.Interfaces;
using InvestmentPortfolioTracker.Business.Notifications;

namespace InvestmentPortfolioTracker.Api.Configuration;

public static class DependencyInjectionConfig
{
    public static IServiceCollection ResolveDependencies(this IServiceCollection services)
    {
        #region Notification
        services.AddScoped<INotifier, Notifier>();
        #endregion

        return services;
    }
}