// CSV取込処理（検証/重複判定/ログ出力/投入判定）
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using PortfolioApp.Models;

namespace PortfolioApp.Services
{
    public class WorkLogImportService
    {
        private readonly WorkLogRepository _repository;

        public WorkLogImportService(WorkLogRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// CSVファイルを取り込む
        /// </summary>
        /// <param name="csvPath">CSVファイルパス</param>
        /// <param name="isDryRun">ドライランモード（DB投入しない）</param>
        /// <returns>取込結果</returns>
        public async Task<ImportResult> ImportAsync(string csvPath, bool isDryRun)
        {
            var result = new ImportResult();
            var csvDirectory = Path.GetDirectoryName(csvPath) ?? "";

            // 一時データリスト（CSV読込結果）
            var tempRecords = new List<WorkLogRecord>();
            var skippedRecords = new List<SkippedRecord>();
            var processedKeys = new HashSet<string>();
            int totalCount = 0;

            // CSVファイル読み込み
            using (var reader = new StreamReader(csvPath, Encoding.UTF8))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null
            }))
            {
                csv.Read();
                csv.ReadHeader();

                while (csv.Read())
                {
                    totalCount++;
                    int rowNumber = totalCount + 1; // ヘッダ含む

                    try
                    {
                        // 必須項目の取得
                        var workDateStr = csv.GetField<string>("WorkDate")?.Trim() ?? "";
                        var member = csv.GetField<string>("Member")?.Trim() ?? "";
                        var project = csv.GetField<string>("Project")?.Trim() ?? "";
                        var workType = csv.GetField<string>("WorkType")?.Trim() ?? "";
                        var hoursStr = csv.GetField<string>("Hours")?.Trim() ?? "";
                        var hourlyRateStr = csv.GetField<string>("HourlyRate")?.Trim() ?? "";

                        // 必須チェック
                        if (string.IsNullOrEmpty(workDateStr) ||
                            string.IsNullOrEmpty(member) ||
                            string.IsNullOrEmpty(project) ||
                            string.IsNullOrEmpty(workType) ||
                            string.IsNullOrEmpty(hoursStr))
                        {
                            skippedRecords.Add(new SkippedRecord
                            {
                                RowNumber = rowNumber,
                                Reason = SkipReason.RequiredMissing,
                                WorkDate = workDateStr,
                                Member = member,
                                Project = project,
                                WorkType = workType,
                                Hours = hoursStr,
                                HourlyRate = hourlyRateStr
                            });
                            continue;
                        }

                        // 型変換チェック
                        if (!DateTime.TryParse(workDateStr, out DateTime workDate))
                        {
                            skippedRecords.Add(new SkippedRecord
                            {
                                RowNumber = rowNumber,
                                Reason = SkipReason.InvalidWorkDate,
                                WorkDate = workDateStr,
                                Member = member,
                                Project = project,
                                WorkType = workType,
                                Hours = hoursStr,
                                HourlyRate = hourlyRateStr
                            });
                            continue;
                        }

                        if (!decimal.TryParse(hoursStr, out decimal hours))
                        {
                            skippedRecords.Add(new SkippedRecord
                            {
                                RowNumber = rowNumber,
                                Reason = SkipReason.InvalidHours,
                                WorkDate = workDateStr,
                                Member = member,
                                Project = project,
                                WorkType = workType,
                                Hours = hoursStr,
                                HourlyRate = hourlyRateStr
                            });
                            continue;
                        }

                        // HourlyRate は任意
                        int? hourlyRate = null;
                        if (!string.IsNullOrEmpty(hourlyRateStr) && int.TryParse(hourlyRateStr, out int rate))
                        {
                            hourlyRate = rate;
                        }

                        // ファイル内重複チェック
                        var key = $"{workDate:yyyy-MM-dd}|{member}|{project}|{workType}";
                        if (processedKeys.Contains(key))
                        {
                            skippedRecords.Add(new SkippedRecord
                            {
                                RowNumber = rowNumber,
                                Reason = SkipReason.DuplicateInFile,
                                WorkDate = workDateStr,
                                Member = member,
                                Project = project,
                                WorkType = workType,
                                Hours = hoursStr,
                                HourlyRate = hourlyRateStr
                            });
                            continue;
                        }

                        processedKeys.Add(key);

                        // 一時リストに追加
                        tempRecords.Add(new WorkLogRecord
                        {
                            RowNumber = rowNumber,
                            WorkDate = workDate,
                            Member = member,
                            Project = project,
                            WorkType = workType,
                            Hours = hours,
                            HourlyRate = hourlyRate
                        });
                    }
                    catch
                    {
                        skippedRecords.Add(new SkippedRecord
                        {
                            RowNumber = rowNumber,
                            Reason = SkipReason.ParseError,
                            WorkDate = csv.GetField<string>("WorkDate") ?? "",
                            Member = csv.GetField<string>("Member") ?? "",
                            Project = csv.GetField<string>("Project") ?? "",
                            WorkType = csv.GetField<string>("WorkType") ?? "",
                            Hours = csv.GetField<string>("Hours") ?? "",
                            HourlyRate = csv.GetField<string>("HourlyRate") ?? ""
                        });
                    }
                }
            }

            result.TotalRead = totalCount;

            // DB重複チェック
            var existingKeys = await _repository.GetExistingKeysAsync(tempRecords);

            // 重複除外してDataTable作成
            var dataTable = new DataTable();
            dataTable.Columns.Add("WorkDate", typeof(DateTime));
            dataTable.Columns.Add("Member", typeof(string));
            dataTable.Columns.Add("Project", typeof(string));
            dataTable.Columns.Add("WorkType", typeof(string));
            dataTable.Columns.Add("Hours", typeof(decimal));
            dataTable.Columns.Add("HourlyRate", typeof(int));

            foreach (var record in tempRecords)
            {
                var key = $"{record.WorkDate:yyyy-MM-dd}|{record.Member}|{record.Project}|{record.WorkType}";
                if (existingKeys.Contains(key))
                {
                    // DB重複
                    skippedRecords.Add(new SkippedRecord
                    {
                        RowNumber = record.RowNumber,
                        Reason = SkipReason.DuplicateInDb,
                        WorkDate = record.WorkDate.ToString("yyyy-MM-dd"),
                        Member = record.Member,
                        Project = record.Project,
                        WorkType = record.WorkType,
                        Hours = record.Hours.ToString(),
                        HourlyRate = record.HourlyRate?.ToString() ?? ""
                    });
                }
                else
                {
                    // 投入対象
                    dataTable.Rows.Add(
                        record.WorkDate,
                        record.Member,
                        record.Project,
                        record.WorkType,
                        record.Hours,
                        record.HourlyRate.HasValue ? (object)record.HourlyRate.Value : DBNull.Value
                    );
                }
            }

            result.PlannedInsert = dataTable.Rows.Count;

            // ドライランでない場合のみ投入
            int actualInsertedCount = 0;
            if (!isDryRun && dataTable.Rows.Count > 0)
            {
                await _repository.BulkInsertAsync(dataTable);
                actualInsertedCount = dataTable.Rows.Count;
            }

            result.Inserted = actualInsertedCount;
            result.SkippedRecords = skippedRecords;

            // スキップカウント集計
            result.RequiredMissing = skippedRecords.Count(r => r.Reason == SkipReason.RequiredMissing);
            result.InvalidWorkDate = skippedRecords.Count(r => r.Reason == SkipReason.InvalidWorkDate);
            result.InvalidHours = skippedRecords.Count(r => r.Reason == SkipReason.InvalidHours);
            result.DuplicateInFile = skippedRecords.Count(r => r.Reason == SkipReason.DuplicateInFile);
            result.DuplicateInDb = skippedRecords.Count(r => r.Reason == SkipReason.DuplicateInDb);

            // エラーレポート出力
            if (skippedRecords.Count > 0)
            {
                result.ErrorLogPath = SaveErrorReport(csvDirectory, skippedRecords);
            }

            return result;
        }

        /// <summary>
        /// エラーレポートCSV出力
        /// </summary>
        private string? SaveErrorReport(string directory, List<SkippedRecord> records)
        {
            if (records.Count == 0) return null;

            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"import_errors_{timestamp}.csv";
                var filePath = Path.Combine(directory, fileName);

                var csv = new StringBuilder();
                csv.AppendLine("RowNumber,Reason,WorkDate,Member,Project,WorkType,Hours,HourlyRate");

                foreach (var record in records.OrderBy(r => r.RowNumber))
                {
                    csv.AppendLine($"{record.RowNumber},{record.GetReasonText()},{record.WorkDate},{record.Member},{record.Project},{record.WorkType},{record.Hours},{record.HourlyRate}");
                }

                File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
                return filePath;
            }
            catch
            {
                return null;
            }
        }
    }
}
