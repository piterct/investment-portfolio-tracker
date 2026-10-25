using InvestmentPortfolioTracker.Business.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace InvestmentPortfolioTracker.Repository.DataContext
{
    public class InvestmentPortfolioContext
    {
        private string? DatabaseDirectory { get; } 

        public string ConnectionString { get;}

        public InvestmentPortfolioContext(IOptions<Settings> config, IHostEnvironment environment)
        {
            var configuredDirectory = config.Value.Directory;

                DatabaseDirectory = string.IsNullOrWhiteSpace(configuredDirectory) ? environment.ContentRootPath :
                    Path.IsPathRooted(configuredDirectory) ? configuredDirectory :
                    Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredDirectory));

            ConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DatabaseDirectory,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
        }
        public SqliteConnection CreateConnection()
        {
            return new SqliteConnection(ConnectionString);
        }
    }
}
