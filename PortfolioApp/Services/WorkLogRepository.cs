// WorkLogsテーブルへのDBアクセスを集約
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using PortfolioApp.Models;

namespace PortfolioApp.Services
{
    public class WorkLogRepository
    {
        private readonly string _connectionString;

        private const string SqlMonthly =
            "SELECT CONVERT(char(7), WorkDate, 120) AS [Month], SUM(Hours) AS TotalHours " +
            "FROM dbo.WorkLogs " +
            "WHERE WorkDate >= @From AND WorkDate < @To " +
            "GROUP BY CONVERT(char(7), WorkDate, 120) " +
            "ORDER BY [Month];";

        private const string SqlProject =
            "SELECT TOP (10) Project, SUM(Hours) AS TotalHours " +
            "FROM dbo.WorkLogs " +
            "WHERE WorkDate >= @From AND WorkDate < @To " +
            "GROUP BY Project " +
            "ORDER BY TotalHours DESC;";

        private const string SqlMember =
            "SELECT TOP (10) Member, SUM(Hours) AS TotalHours " +
            "FROM dbo.WorkLogs " +
            "WHERE WorkDate >= @From AND WorkDate < @To " +
            "GROUP BY Member " +
            "ORDER BY TotalHours DESC;";

        public WorkLogRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// WorkLogsテーブルの最小・最大日付を取得
        /// </summary>
        public async Task<(DateTime? min, DateTime? max)> GetWorkDateRangeAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT MIN(WorkDate) AS MinDate, MAX(WorkDate) AS MaxDate FROM dbo.WorkLogs", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                {
                    return (reader.GetDateTime(0), reader.GetDateTime(1));
                }
            }

            return (null, null);
        }

        /// <summary>
        /// 指定されたレコードのキーがDBに既に存在するかをチェック
        /// キー形式: yyyy-MM-dd|Member|Project|WorkType
        /// </summary>
        public async Task<HashSet<string>> GetExistingKeysAsync(IEnumerable<WorkLogRecord> records)
        {
            var existingKeys = new HashSet<string>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // 一時テーブルを作成してキーを投入し、JOINで既存キーを抽出
            var tempTable = "#TempKeys";
            var createTempSql = $@"
                CREATE TABLE {tempTable} (
                    WorkDate date,
                    Member nvarchar(50),
                    Project nvarchar(100),
                    WorkType nvarchar(50)
                )";

            using (var createCmd = new SqlCommand(createTempSql, conn))
            {
                await createCmd.ExecuteNonQueryAsync();
            }

            // 一時テーブルにデータ投入
            foreach (var record in records)
            {
                var insertSql = $@"
                    INSERT INTO {tempTable} (WorkDate, Member, Project, WorkType)
                    VALUES (@WorkDate, @Member, @Project, @WorkType)";

                using var insertCmd = new SqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@WorkDate", record.WorkDate);
                insertCmd.Parameters.AddWithValue("@Member", record.Member);
                insertCmd.Parameters.AddWithValue("@Project", record.Project);
                insertCmd.Parameters.AddWithValue("@WorkType", record.WorkType);

                await insertCmd.ExecuteNonQueryAsync();
            }

            // 既存キーを抽出
            var selectSql = $@"
                SELECT CONCAT(CONVERT(varchar, t.WorkDate, 23), '|', t.Member, '|', t.Project, '|', t.WorkType) AS KeyValue
                FROM {tempTable} t
                INNER JOIN dbo.WorkLogs w
                    ON t.WorkDate = w.WorkDate
                    AND t.Member = w.Member
                    AND t.Project = w.Project
                    AND t.WorkType = w.WorkType";

            using (var selectCmd = new SqlCommand(selectSql, conn))
            using (var reader = await selectCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingKeys.Add(reader.GetString(0));
                }
            }

            return existingKeys;
        }

        /// <summary>
        /// 一括投入
        /// </summary>
        public async Task BulkInsertAsync(DataTable dataTable)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var bulkCopy = new SqlBulkCopy(conn);
            bulkCopy.DestinationTableName = "dbo.WorkLogs";
            bulkCopy.ColumnMappings.Add("WorkDate", "WorkDate");
            bulkCopy.ColumnMappings.Add("Member", "Member");
            bulkCopy.ColumnMappings.Add("Project", "Project");
            bulkCopy.ColumnMappings.Add("WorkType", "WorkType");
            bulkCopy.ColumnMappings.Add("Hours", "Hours");
            bulkCopy.ColumnMappings.Add("HourlyRate", "HourlyRate");

            await bulkCopy.WriteToServerAsync(dataTable);
        }

        /// <summary>
        /// 月別集計を取得
        /// </summary>
        public async Task<List<MonthlyHoursRow>> GetMonthlyAsync(DateTime from, DateTime to)
        {
            var rows = new List<MonthlyHoursRow>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(SqlMonthly, conn);
            cmd.Parameters.AddWithValue("@From", from);
            cmd.Parameters.AddWithValue("@To", to);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new MonthlyHoursRow
                {
                    Month = reader.GetString(0),
                    TotalHours = Convert.ToDecimal(reader[1])
                });
            }

            return rows;
        }

        /// <summary>
        /// 案件別集計（TOP 10）を取得
        /// </summary>
        public async Task<List<ProjectHoursRow>> GetProjectTopAsync(DateTime from, DateTime to)
        {
            var rows = new List<ProjectHoursRow>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(SqlProject, conn);
            cmd.Parameters.AddWithValue("@From", from);
            cmd.Parameters.AddWithValue("@To", to);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new ProjectHoursRow
                {
                    Project = reader.GetString(0),
                    TotalHours = Convert.ToDecimal(reader[1])
                });
            }

            return rows;
        }

        /// <summary>
        /// メンバー別集計（TOP 10）を取得
        /// </summary>
        public async Task<List<MemberHoursRow>> GetMemberTopAsync(DateTime from, DateTime to)
        {
            var rows = new List<MemberHoursRow>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(SqlMember, conn);
            cmd.Parameters.AddWithValue("@From", from);
            cmd.Parameters.AddWithValue("@To", to);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new MemberHoursRow
                {
                    Member = reader.GetString(0),
                    TotalHours = Convert.ToDecimal(reader[1])
                });
            }

            return rows;
        }
    }
}
