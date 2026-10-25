using InvestmentPortfolioTracker.Business.Notifications;
using InvestmentPortfolioTracker.Business.Repositories;
using InvestmentPortfolioTracker.Business.Shared;
using InvestmentPortfolioTracker.Repository.DataContext;
using InvestmentPortfolioTracker.Repository.Repositories;

namespace InvestmentPortfolioTracker.Api.Configuration;

public static class DependencyInjectionConfig
{
    public static IServiceCollection ResolveDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        #region DbContext
        services.Configure<Settings>(configuration.GetSection("Database"));
        #endregion

        #region Notification
        services.AddScoped<INotifier, Notifier>();
        #endregion

        #region Configurations
        services.AddScoped<InvestmentPortfolioContext, InvestmentPortfolioContext>();
        #endregion

        #region Repositories
        services.AddTransient<ISettingsRepository, SettingsRepository>();
        #endregion

        return services;
    }
}