// CSV読み込み用の一時データモデル
using System;

namespace PortfolioApp.Models
{
    public class WorkLogRecord
    {
        public int RowNumber { get; set; }
        public DateTime WorkDate { get; set; }
        public string Member { get; set; } = "";
        public string Project { get; set; } = "";
        public string WorkType { get; set; } = "";
        public decimal Hours { get; set; }
        public int? HourlyRate { get; set; }
    }
}
