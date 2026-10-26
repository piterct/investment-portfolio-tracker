using InvestmentPortfolioTracker.Api.Configuration;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace InvestmentPortfolioTracker.Api
{
    public class Startup: IStartupProject
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.WebApiConfig();
            services.AddSwaggerConfig();
            services.ResolveDependencies(Configuration);
        }

        public void Configure(WebApplication app, IWebHostEnvironment environment, IApiVersionDescriptionProvider provider)
        {
            if (environment.IsDevelopment())
            {
                app.UseCors("Development");
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseCors("Production");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseSwaggerConfig(provider);

            app.MapControllers();
        }
    }

    public interface IStartupProject
    {
        IConfiguration Configuration { get; }
        void Configure(WebApplication app, IWebHostEnvironment environment, IApiVersionDescriptionProvider provider);
        void ConfigureServices(IServiceCollection services);
    }
}
