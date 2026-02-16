// 集計結果のCSV出力
using System.Collections.Generic;
using System.IO;
using System.Text;
using PortfolioApp.Models;

namespace PortfolioApp.Services
{
    public class CsvExportService
    {
        /// <summary>
        /// 月別集計をCSV出力
        /// </summary>
        public void ExportMonthly(IEnumerable<MonthlyHoursRow> rows, string path)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Month,TotalHours");

            foreach (var row in rows)
            {
                csv.AppendLine($"{row.Month},{row.TotalHours}");
            }

            File.WriteAllText(path, csv.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 案件別集計をCSV出力
        /// </summary>
        public void ExportProject(IEnumerable<ProjectHoursRow> rows, string path)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Project,TotalHours");

            foreach (var row in rows)
            {
                csv.AppendLine($"{row.Project},{row.TotalHours}");
            }

            File.WriteAllText(path, csv.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// メンバー別集計をCSV出力
        /// </summary>
        public void ExportMember(IEnumerable<MemberHoursRow> rows, string path)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Member,TotalHours");

            foreach (var row in rows)
            {
                csv.AppendLine($"{row.Member},{row.TotalHours}");
            }

            File.WriteAllText(path, csv.ToString(), Encoding.UTF8);
        }
    }
}
