// CSV取込でスキップされた理由
namespace PortfolioApp.Models
{
    public enum SkipReason
    {
        RequiredMissing,
        InvalidWorkDate,
        InvalidHours,
        DuplicateInFile,
        DuplicateInDb,
        ParseError
    }
}
