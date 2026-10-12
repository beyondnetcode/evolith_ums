-- =============================================================================
-- Migration: 20260523_add_rowversion_optimistic_concurrency
-- Purpose  : Add SQL Server rowversion (timestamp) column to all aggregate-root
--            tables to enable EF Core optimistic concurrency control (FIX-03).
--
-- The rowversion type is auto-incremented by SQL Server on every INSERT/UPDATE.
-- EF Core reads it on load and includes it in the WHERE clause of UPDATE/DELETE
-- statements; a mismatch (0 rows affected) triggers DbUpdateConcurrencyException,
-- which each repository converts to ConcurrencyConflictException → HTTP 409.
--
-- Safe to re-run: uses IF NOT EXISTS / column existence checks.
-- =============================================================================

-- Identity context --------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[ums_identity].[Tenants]')
      AND name = N'RowVersion'
)
BEGIN
    ALTER TABLE [ums_identity].[Tenants]
    ADD [RowVersion] rowversion NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[ums_identity].[UserAccounts]')
      AND name = N'RowVersion'
)
BEGIN
    ALTER TABLE [ums_identity].[UserAccounts]
    ADD [RowVersion] rowversion NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[ums_identity].[UserManagementDelegations]')
      AND name = N'RowVersion'
)
BEGIN
    ALTER TABLE [ums_identity].[UserManagementDelegations]
    ADD [RowVersion] rowversion NOT NULL;
END
GO

-- Authorization context ---------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[ums_authorization].[Profiles]')
      AND name = N'RowVersion'
)
BEGIN
    ALTER TABLE [ums_authorization].[Profiles]
    ADD [RowVersion] rowversion NOT NULL;
END
GO

-- Configuration context ---------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[ums_configuration].[AppConfigurations]')
      AND name = N'RowVersion'
)
BEGIN
    ALTER TABLE [ums_configuration].[AppConfigurations]
    ADD [RowVersion] rowversion NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[ums_configuration].[FeatureFlags]')
      AND name = N'RowVersion'
)
BEGIN
    ALTER TABLE [ums_configuration].[FeatureFlags]
    ADD [RowVersion] rowversion NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[ums_configuration].[IdpConfigurations]')
      AND name = N'RowVersion'
)
BEGIN
    ALTER TABLE [ums_configuration].[IdpConfigurations]
    ADD [RowVersion] rowversion NOT NULL;
END
GO
