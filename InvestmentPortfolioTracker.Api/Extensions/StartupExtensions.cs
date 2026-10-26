using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace InvestmentPortfolioTracker.Api.Extensions
{
    public static class StartupExtensions
    {
        public static WebApplicationBuilder UseStartup<TStartup>(this WebApplicationBuilder webApplicationBuilder) where TStartup : IStartupProject
        {
            var startup = Activator.CreateInstance(typeof(TStartup), webApplicationBuilder.Configuration) as IStartupProject;
            if (startup == null) throw new ArgumentException("Invalid Startup.cs class");


            startup.ConfigureServices(webApplicationBuilder.Services);

            var app = webApplicationBuilder.Build();

            var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

            startup.Configure(app, app.Environment, provider);

            app.Run();

            return webApplicationBuilder;
        }
    }
}
