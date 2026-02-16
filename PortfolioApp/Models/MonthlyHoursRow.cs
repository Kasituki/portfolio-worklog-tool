// 月別工数の集計結果
namespace PortfolioApp.Models
{
    public class MonthlyHoursRow
    {
        public string Month { get; set; } = "";
        public decimal TotalHours { get; set; }
    }
}
