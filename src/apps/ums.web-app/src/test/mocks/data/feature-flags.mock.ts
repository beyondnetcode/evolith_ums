/**
 * feature-flags.mock.ts — Mock data matching ConfigurationDevDataSeeder exactly.
 *
 * Covers all evaluation scenarios defined in ADR-0068 and the FeatureFlagCriteria docs:
 *   1. No criteria        — active for all callers (MAINTENANCE_MODE)
 *   2. TenantId criteria  — scoped to a single tenant (ENABLE_MFA)
 *   3. RoleCode criteria  — In operator with multiple values (ADVANCED_REPORTING)
 *   4. DateRange criteria — time-boxed rollout (NEW_AUDIT_DASHBOARD)
 *   5. Multi-type AND     — TenantId AND RoleCode (PREMIUM_REPORTS)
 *   6. Percentage type    — probabilistic rollout (AUTO_REORDER)
 */
import type { FeatureFlag } from '@domain/configuration/models/feature-flag.model';

// ── Shared constants (match CoreDevDataSeeder) ─────────────────────────────
const RANSA_TENANT_ID = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
const LOGISTICS_SUITE_ID = 'dddd0001-0000-0000-0000-000000000001'; // DemoSystemSuiteId
const WMS_SUITE_ID = 'dddd0002-0000-0000-0000-000000000002'; // placeholder for WMS

// ── Scenario 1: No criteria — active for all (MAINTENANCE_MODE) ────────────
export const mockFlagMaintenanceMode: FeatureFlag = {
  featureFlagId: 'ff000001-0000-0000-0000-000000000001',
  systemSuiteId: LOGISTICS_SUITE_ID,
  flagCode: 'MAINTENANCE_MODE',
  flagType: 'Boolean',
  flagTargets: '*',
  status: 'Inactive',
  rolloutPercentage: null,
  criteria: [],
};

// ── Scenario 2: TenantId Equals — tenant-scoped MFA (ENABLE_MFA) ──────────
export const mockFlagEnableMfa: FeatureFlag = {
  featureFlagId: 'ff000002-0000-0000-0000-000000000002',
  systemSuiteId: LOGISTICS_SUITE_ID,
  flagCode: 'ENABLE_MFA',
  flagType: 'Boolean',
  flagTargets: '*',
  status: 'Active',
  rolloutPercentage: null,
  criteria: [
    {
      criteriaId: 'c0000001-0000-0000-0000-000000000001',
      criteriaType: 'TenantId',
      operator: 'Equals',
      value: RANSA_TENANT_ID,
      createdAtUtc: '2026-05-27T00:00:00Z',
    },
  ],
};

// ── Scenario 3: RoleCode In — role-gated feature (ADVANCED_REPORTING) ─────
export const mockFlagAdvancedReporting: FeatureFlag = {
  featureFlagId: 'ff000003-0000-0000-0000-000000000003',
  systemSuiteId: LOGISTICS_SUITE_ID,
  flagCode: 'ADVANCED_REPORTING',
  flagType: 'Boolean',
  flagTargets: 'role:ADMIN,role:SUPERVISOR',
  status: 'Inactive',
  rolloutPercentage: null,
  criteria: [
    {
      criteriaId: 'c0000002-0000-0000-0000-000000000002',
      criteriaType: 'RoleCode',
      operator: 'In',
      value: '["ADMIN","SUPERVISOR"]',
      createdAtUtc: '2026-05-27T00:00:00Z',
    },
  ],
};

// ── Scenario 4: DateRange — time-boxed rollout (NEW_AUDIT_DASHBOARD) ──────
export const mockFlagNewAuditDashboard: FeatureFlag = {
  featureFlagId: 'ff000004-0000-0000-0000-000000000004',
  systemSuiteId: LOGISTICS_SUITE_ID,
  flagCode: 'NEW_AUDIT_DASHBOARD',
  flagType: 'Boolean',
  flagTargets: '*',
  status: 'Active',
  rolloutPercentage: null,
  criteria: [
    {
      criteriaId: 'c0000003-0000-0000-0000-000000000003',
      criteriaType: 'DateRange',
      operator: 'Between',
      value: '{"from":"2026-01-01T00:00:00Z","to":"2026-12-31T23:59:59Z"}',
      createdAtUtc: '2026-05-27T00:00:00Z',
    },
  ],
};

// ── Scenario 5: Multi-type AND — TenantId AND RoleCode (PREMIUM_REPORTS) ──
// Evaluation: (TenantId == ransa) AND (RoleCode IN [ADMIN, SUPERVISOR])
export const mockFlagPremiumReports: FeatureFlag = {
  featureFlagId: 'ff000005-0000-0000-0000-000000000005',
  systemSuiteId: LOGISTICS_SUITE_ID,
  flagCode: 'PREMIUM_REPORTS',
  flagType: 'Boolean',
  flagTargets: '*',
  status: 'Active',
  rolloutPercentage: null,
  criteria: [
    {
      criteriaId: 'c0000004-0000-0000-0000-000000000004',
      criteriaType: 'TenantId',
      operator: 'Equals',
      value: RANSA_TENANT_ID,
      createdAtUtc: '2026-05-27T00:00:00Z',
    },
    {
      criteriaId: 'c0000005-0000-0000-0000-000000000005',
      criteriaType: 'RoleCode',
      operator: 'In',
      value: '["ADMIN","SUPERVISOR"]',
      createdAtUtc: '2026-05-27T00:00:00Z',
    },
  ],
};

// ── Scenario 6: Percentage rollout — probabilistic (AUTO_REORDER, WMS) ───
export const mockFlagAutoReorder: FeatureFlag = {
  featureFlagId: 'ff000006-0000-0000-0000-000000000006',
  systemSuiteId: WMS_SUITE_ID,
  flagCode: 'AUTO_REORDER',
  flagType: 'Percentage',
  flagTargets: '*',
  status: 'Inactive',
  rolloutPercentage: 50,
  criteria: [],
};

// ── Composite export ───────────────────────────────────────────────────────

/** All mock feature flags — order matches seeder priority */
export const mockFeatureFlags: FeatureFlag[] = [
  mockFlagMaintenanceMode,
  mockFlagEnableMfa,
  mockFlagAdvancedReporting,
  mockFlagNewAuditDashboard,
  mockFlagPremiumReports,
  mockFlagAutoReorder,
];

/** Only LOGISTICS_CORE flags */
export const mockLogisticsCoreFeatureFlags: FeatureFlag[] = [
  mockFlagMaintenanceMode,
  mockFlagEnableMfa,
  mockFlagAdvancedReporting,
  mockFlagNewAuditDashboard,
  mockFlagPremiumReports,
];

/** Only WMS flags */
export const mockWmsFeatureFlags: FeatureFlag[] = [mockFlagAutoReorder];
