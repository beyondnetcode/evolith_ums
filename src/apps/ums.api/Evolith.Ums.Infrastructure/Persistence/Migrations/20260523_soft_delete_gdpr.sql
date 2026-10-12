-- =============================================================================
-- Migration: 20260523_soft_delete_gdpr.sql
-- REC-16: Soft-delete + GDPR anonymization columns for Tenants and UserAccounts
-- =============================================================================
-- Tenants: add IsDeleted, DeletedAtUtc, DeletedBy
-- UserAccounts: add IsDeleted, DeletedAtUtc, DeletedBy, AnonymizedAtUtc
-- Filtered indexes on IsDeleted = 0 for query performance
-- =============================================================================

-- -----------------------------------------------------------------------
-- [ums_identity].[Tenants]
-- -----------------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[ums_identity].[Tenants]')
      AND name = 'IsDeleted'
)
BEGIN
    ALTER TABLE [ums_identity].[Tenants]
        ADD [IsDeleted]    BIT           NOT NULL DEFAULT 0,
            [DeletedAtUtc] DATETIME2(7)  NULL,
            [DeletedBy]    NVARCHAR(100) NULL;

    PRINT 'Added soft-delete columns to [ums_identity].[Tenants]';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('[ums_identity].[Tenants]')
      AND name = 'IX_Tenants_IsDeleted'
)
BEGIN
    CREATE INDEX [IX_Tenants_IsDeleted]
        ON [ums_identity].[Tenants]([IsDeleted])
        WHERE [IsDeleted] = 0;

    PRINT 'Created filtered index IX_Tenants_IsDeleted';
END
GO

-- -----------------------------------------------------------------------
-- [ums_identity].[UserAccounts]
-- -----------------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[ums_identity].[UserAccounts]')
      AND name = 'IsDeleted'
)
BEGIN
    ALTER TABLE [ums_identity].[UserAccounts]
        ADD [IsDeleted]       BIT           NOT NULL DEFAULT 0,
            [DeletedAtUtc]    DATETIME2(7)  NULL,
            [DeletedBy]       NVARCHAR(100) NULL,
            -- GDPR: timestamp set when email and IdentityReference are replaced
            -- with anonymized values. Allows audit of when anonymization occurred.
            [AnonymizedAtUtc] DATETIME2(7)  NULL;

    PRINT 'Added soft-delete + GDPR columns to [ums_identity].[UserAccounts]';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('[ums_identity].[UserAccounts]')
      AND name = 'IX_UserAccounts_IsDeleted'
)
BEGIN
    CREATE INDEX [IX_UserAccounts_IsDeleted]
        ON [ums_identity].[UserAccounts]([IsDeleted])
        WHERE [IsDeleted] = 0;

    PRINT 'Created filtered index IX_UserAccounts_IsDeleted';
END
GO

PRINT '20260523_soft_delete_gdpr.sql completed successfully';
GO
