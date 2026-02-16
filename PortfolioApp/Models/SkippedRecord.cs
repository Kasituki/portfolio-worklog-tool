// CSV取込でスキップされたレコード情報
namespace PortfolioApp.Models
{
    public class SkippedRecord
    {
        public int RowNumber { get; set; }
        public SkipReason Reason { get; set; }
        public string WorkDate { get; set; } = "";
        public string Member { get; set; } = "";
        public string Project { get; set; } = "";
        public string WorkType { get; set; } = "";
        public string Hours { get; set; } = "";
        public string HourlyRate { get; set; } = "";

        public string GetReasonText()
        {
            return Reason switch
            {
                SkipReason.RequiredMissing => "required_missing",
                SkipReason.InvalidWorkDate => "invalid_workdate",
                SkipReason.InvalidHours => "invalid_hours",
                SkipReason.DuplicateInFile => "duplicate_in_file",
                SkipReason.DuplicateInDb => "duplicate_in_db",
                SkipReason.ParseError => "parse_error",
                _ => "unknown"
            };
        }
    }
}
