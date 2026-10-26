namespace InvestmentPortfolioTracker.Business.Models
{
    public class AppSettings : Entity
    {
        public AppSettings(int id, decimal patrimony, string? apiToken)
        {
            Id = id;
            Patrimony = patrimony;
            ApiToken = apiToken;
        }
        public decimal Patrimony { get; set; }
        public string? ApiToken { get; set; }
    }
}
