/*
    ClarityClaim-Database — 01_CreateDatabase.sql
    Creates the ClarityClaimDb database. Run this first, in SSMS, connected to
    your local SQL Server instance (Windows Authentication is fine for local dev
    — see the screenshot server name LAPTOP-M53Q5L8P).

    Safe to re-run: skips creation if the database already exists.
*/

IF DB_ID(N'ClarityClaimDb') IS NULL
BEGIN
    CREATE DATABASE ClarityClaimDb;
END
GO

ALTER DATABASE ClarityClaimDb SET RECOVERY SIMPLE;
GO
