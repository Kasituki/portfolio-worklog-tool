IF OBJECT_ID(N'dbo.WorkLogs', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.WorkLogs;
END
GO

CREATE TABLE dbo.WorkLogs
(
    WorkLogId  int IDENTITY(1,1) NOT NULL PRIMARY KEY,

    -- WorkLogRecord.WorkDate (DateTime) -> DBは日付だけでよいので date
    WorkDate   date           NOT NULL,

    -- string
    Member     nvarchar(100)  NOT NULL,
    Project    nvarchar(200)  NOT NULL,
    WorkType   nvarchar(100)  NOT NULL,

    -- WorkLogRecord.Hours (decimal)
    Hours      decimal(9,2)   NOT NULL,

    -- WorkLogRecord.HourlyRate (int?)
    HourlyRate int            NULL,

    CreatedAt  datetime2(0)   NOT NULL CONSTRAINT DF_WorkLogs_CreatedAt DEFAULT (sysdatetime()),
    UpdatedAt  datetime2(0)   NOT NULL CONSTRAINT DF_WorkLogs_UpdatedAt DEFAULT (sysdatetime())
);

-- WorkLogsテーブル作成の後に追加
IF NOT EXISTS (
  SELECT 1
  FROM sys.indexes
  WHERE name = 'UX_WorkLogs_Key'
    AND object_id = OBJECT_ID('dbo.WorkLogs')
)
BEGIN
  CREATE UNIQUE INDEX UX_WorkLogs_Key
    ON dbo.WorkLogs (WorkDate, Member, Project, WorkType);
END
GO
