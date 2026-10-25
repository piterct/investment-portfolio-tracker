using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace InvestmentPortfolioTracker.Api.Configuration
{
    public static class ApiConfig
    {
        public static IServiceCollection WebApiConfig(this IServiceCollection services)
        {
            services.AddControllersWithViews()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                });

            services.AddApiVersioning(options =>
            {
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.ReportApiVersions = true;
            });

            services.AddVersionedApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });

            services.AddCors(options =>
                {
                    options.AddPolicy("Development",
                        builder => builder.AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader());

                    options.AddPolicy("Production",
                        builder =>
                            builder
                                .WithMethods("GET")
                                .WithOrigins("http://google.com.br")
                                .SetIsOriginAllowedToAllowWildcardSubdomains()
                                .AllowAnyHeader());
                }

            );

            return services;
        }
    }
}
