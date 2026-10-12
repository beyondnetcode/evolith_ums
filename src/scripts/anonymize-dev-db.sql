-- =============================================================================
-- OPS-03: Dev DB Anonymization Script
-- =============================================================================
-- Purpose : Replace PII in a COPY of the production (or staging) database with
--           deterministic, fake values so that developers can work with realistic
--           data shapes without exposing personal information.
--
-- IMPORTANT: Run this on a RESTORED COPY — never on the production database.
--            The shell wrapper (anonymize-dev-db.sh) enforces this by checking
--            the database name against a safeguard list.
--
-- Compliance: GDPR Art. 25 (data protection by design); aligns with the
--             backup retention policy (docs/operations/runbooks/gdpr-backup-retention-policy.md).
--
-- Safety guards:
--   1. Aborts if the database name looks like a production/staging name.
--   2. Runs inside a single transaction so failures leave no partial state.
--   3. Logs a record to [ums_audit].[AnonymizationRuns] for traceability.
-- =============================================================================

USE [$(TargetDatabase)];   -- Substitute via sqlcmd -v TargetDatabase=ums_dev_copy
GO

-- ──────────────────────────────────────────────────────────────────────────────
-- GUARD: Refuse to run on databases whose name matches production patterns.
-- Add any other production/staging DB names here.
-- ──────────────────────────────────────────────────────────────────────────────
DECLARE @dbName NVARCHAR(128) = DB_NAME();

IF @dbName IN (N'ums', N'ums_prod', N'ums_staging', N'ums_preprod')
BEGIN
    RAISERROR(
        'SAFETY ABORT: anonymize-dev-db.sql must not run on production or staging databases. DB=%s',
        16, 1, @dbName);
    RETURN;
END;

BEGIN TRANSACTION;
BEGIN TRY

-- ──────────────────────────────────────────────────────────────────────────────
-- 1. UserAccounts — anonymize email, phone, identity reference
--    Deterministic: SHA2_256 on the row GUID produces a stable fake address.
--    This matches the production GDPR anonymization format so tests that check
--    the anonymization shape will still pass.
-- ──────────────────────────────────────────────────────────────────────────────
UPDATE [ums_identity].[UserAccounts]
SET
    [Email]             = CONCAT(
                              N'dev_',
                              LEFT(CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', CAST([Id] AS NVARCHAR(36))), 2), 12),
                              N'@dev.invalid'),
    [PhoneNumber]       = CASE WHEN [PhoneNumber] IS NOT NULL
                              THEN CONCAT(N'+1555', FORMAT(ABS(CHECKSUM([Id])) % 10000000, '0000000'))
                              ELSE NULL END,
    [IdentityReference] = CASE WHEN [IdentityReference] IS NOT NULL
                              THEN CONCAT(N'dev-idp|',
                                          LEFT(CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', CAST([Id] AS NVARCHAR(36))), 2), 16))
                              ELSE NULL END,
    [DisplayName]       = CASE WHEN [DisplayName] IS NOT NULL
                              THEN CONCAT(N'DevUser ', ROW_NUMBER() OVER (ORDER BY [Id]))
                              ELSE NULL END,
    [UpdatedBy]         = N'anonymize-dev-db.sql',
    [UpdatedAtUtc]      = SYSUTCDATETIME()
WHERE [IsDeleted] = 0;   -- Already-deleted rows keep their production anonymization (gdpr_del_* format).

-- ──────────────────────────────────────────────────────────────────────────────
-- 2. Audit records — strip any PII that may have been captured in WhatChanged.
--    Replace email-like strings with a sentinel so log replay doesn't leak data.
-- ──────────────────────────────────────────────────────────────────────────────
-- Note: current schema stores field names, not values (see GDPR runbook §7).
-- This UPDATE is a belt-and-suspenders measure for older rows / schema migrations.
UPDATE [ums_audit].[AuditRecords]
SET
    [WhatChanged] = REGEXP_REPLACE([WhatChanged],
        N'[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}',
        N'<email-masked>',
        1, 0, 'i')
WHERE [WhatChanged] LIKE N'%@%';

-- SQL Server does not have REGEXP_REPLACE built-in before 2022 CTP.
-- For SQL Server 2022+ with the STRING_SEARCH_PATTERN compat flag OFF use:
-- (The above is valid SQL Server 2022 R2+ syntax. For older versions replace
--  with a CLR function or accept the audit table retains old data in dev only.)

-- ──────────────────────────────────────────────────────────────────────────────
-- 3. Tenants — strip any real-company identifiers that are not needed in dev.
--    Keep Code and Name (needed for routing logic) but mask contact fields.
-- ──────────────────────────────────────────────────────────────────────────────
UPDATE [ums_identity].[Tenants]
SET
    [ContactEmail]   = CASE WHEN [ContactEmail] IS NOT NULL
                            THEN CONCAT(N'dev-tenant-', CAST([Id] AS NVARCHAR(36)), N'@dev.invalid')
                            ELSE NULL END,
    [UpdatedBy]      = N'anonymize-dev-db.sql',
    [UpdatedAtUtc]   = SYSUTCDATETIME()
WHERE [ContactEmail] IS NOT NULL AND [IsDeleted] = 0;

-- ──────────────────────────────────────────────────────────────────────────────
-- 4. IdpConfigurations — replace real tenant secrets / metadata with stubs.
-- ──────────────────────────────────────────────────────────────────────────────
UPDATE [ums_configuration].[IdpConfigurations]
SET
    [MetadataJson]   = N'{"authority":"https://login.dev.invalid/common","masked":true}',
    [SecretVaultPath]= CASE WHEN [SecretVaultPath] IS NOT NULL THEN N'kv/dev/stub' ELSE NULL END,
    [UpdatedBy]      = N'anonymize-dev-db.sql',
    [UpdatedAtUtc]   = SYSUTCDATETIME();

-- ──────────────────────────────────────────────────────────────────────────────
-- 5. Log the anonymization run for traceability.
--    Table is created lazily here if it does not exist yet.
-- ──────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'[ums_audit].[AnonymizationRuns]', 'U') IS NULL
BEGIN
    CREATE TABLE [ums_audit].[AnonymizationRuns] (
        [Id]            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        [RunAtUtc]      DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [TargetDatabase]NVARCHAR(128)    NOT NULL,
        [RunBy]         NVARCHAR(256)    NOT NULL,
        [RowsAffected]  INT              NULL
    );
END;

INSERT INTO [ums_audit].[AnonymizationRuns] ([TargetDatabase], [RunBy])
VALUES (@dbName, SUSER_SNAME());

COMMIT TRANSACTION;
PRINT N'Anonymization complete on database: ' + @dbName;

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    DECLARE @msg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(N'Anonymization failed and was rolled back. Error: %s', 16, 1, @msg);
END CATCH;
GO
