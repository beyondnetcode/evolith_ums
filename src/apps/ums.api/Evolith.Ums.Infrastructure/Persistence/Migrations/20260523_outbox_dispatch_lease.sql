-- =============================================================================
-- Migration: 20260523_outbox_dispatch_lease.sql
-- HARDENING-01: Add distributed lease columns to OutboxMessages
-- Prevents duplicate event dispatch in multi-pod (K8s) deployments.
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[ums_platform].[OutboxMessages]')
      AND name = 'LockedUntil'
)
BEGIN
    ALTER TABLE [ums_platform].[OutboxMessages]
        ADD [LockedUntil] DATETIME2(7) NULL,
            [LockedBy]    NVARCHAR(200) NULL;

    PRINT 'Added LockedUntil, LockedBy to [ums_platform].[OutboxMessages]';
END
GO

-- Index to efficiently find claimable messages (Phase 1 of atomic claim).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('[ums_platform].[OutboxMessages]')
      AND name = 'IX_OutboxMessages_Claim'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_Claim]
        ON [ums_platform].[OutboxMessages]([ProcessedOnUtc], [RetryCount], [LockedUntil])
        WHERE [ProcessedOnUtc] IS NULL;

    PRINT 'Created IX_OutboxMessages_Claim index';
END
GO

PRINT '20260523_outbox_dispatch_lease.sql completed successfully';
GO
