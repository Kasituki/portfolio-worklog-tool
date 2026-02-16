-- 01_create_database.sql
IF DB_ID(N'PortfolioDb') IS NULL
BEGIN
    CREATE DATABASE PortfolioDb;
END
GO
