// CSV取込の結果を表すDTO
using System.Collections.Generic;

namespace PortfolioApp.Models
{
    public class ImportResult
    {
        public int TotalRead { get; set; }
        public int Inserted { get; set; }
        public int PlannedInsert { get; set; }
        public int RequiredMissing { get; set; }
        public int InvalidWorkDate { get; set; }
        public int InvalidHours { get; set; }
        public int DuplicateInFile { get; set; }
        public int DuplicateInDb { get; set; }
        public string? ErrorLogPath { get; set; }
        public List<SkippedRecord> SkippedRecords { get; set; } = new();
    }
}
