USE PortfolioDb;
GO

-- 開発/デモ用：データ初期化
-- 依存が無ければTRUNCATEが最速
TRUNCATE TABLE dbo.WorkLogs;
GO

-- ※TRUNCATEできない場合（外部キー等）は以下に差し替え
-- DELETE FROM dbo.WorkLogs;
-- GO
