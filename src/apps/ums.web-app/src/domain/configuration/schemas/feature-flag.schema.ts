/**
 * feature-flag.schema.ts — Runtime validation schemas (Zod)
 *
 * Single source of truth for FeatureFlag types. Validates API responses
 * at the infrastructure boundary so TypeScript types match reality at runtime.
 *
 * Backend enums:
 *   FlagType:   Boolean | Variant | Percentage
 *   FlagStatus: Inactive | Active | Archived
 *   CriteriaType: TenantId | BranchId | UserProfileId | RoleCode | Environment | DateRange | PercentageHash | CustomRule
 *   CriteriaOperator: Equals | NotEquals | In | Between | LessThanOrEqual | Matches
 */
import { z } from 'zod';

// Zod v4 enforces RFC 4122 variant bits in .uuid() — too strict for SQL Server
// GUIDs which may not satisfy those bit constraints. Use a format-only check.
const guidSchema = z
  .string()
  .regex(
    /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/,
    'Invalid GUID format'
  );

export const FlagTypeSchema = z.enum(['Boolean', 'Variant', 'Percentage']);
export const FlagStatusSchema = z.enum(['Inactive', 'Active', 'Archived']);
export const CriteriaTypeSchema = z.enum([
  'TenantId',
  'BranchId',
  'UserProfileId',
  'RoleCode',
  'Environment',
  'DateRange',
  'PercentageHash',
  'CustomRule',
]);
export const CriteriaOperatorSchema = z.enum([
  'Equals',
  'NotEquals',
  'In',
  'Between',
  'LessThanOrEqual',
  'Matches',
]);

export const FeatureFlagCriteriaSchema = z.object({
  criteriaId: guidSchema,
  criteriaType: CriteriaTypeSchema,
  operator: CriteriaOperatorSchema,
  value: z.string(),
  createdAtUtc: z.string(),
});

export const FeatureFlagSchema = z.object({
  featureFlagId: guidSchema,
  systemSuiteId: guidSchema,
  systemSuiteCode: z.string(),
  systemSuiteName: z.string(),
  flagCode: z.string().min(1),
  flagType: FlagTypeSchema,
  flagTargets: z.string(),
  status: FlagStatusSchema,
  linkedResourceType: z.string().nullable().optional(),
  linkedResourceId: guidSchema.nullable().optional(),
  rolloutPercentage: z.number().int().min(0).max(100).nullable().optional(),
  criteria: z.array(FeatureFlagCriteriaSchema).default([]),
});

export const FeatureFlagListSchema = z.array(FeatureFlagSchema);

export const FeatureFlagPageSchema = z.object({
  items: FeatureFlagListSchema,
  page: z.number().int().min(1),
  pageSize: z.number().int().min(1),
  totalItems: z.number().int().min(0),
  totalPages: z.number().int().min(0),
});

// ── Response schemas ──────────────────────────────────────────────────────────

export const CreateFeatureFlagResponseSchema = z.object({
  featureFlagId: guidSchema,
});

export const AddFeatureFlagCriteriaResponseSchema = z.object({
  criteriaId: guidSchema,
});

// ── Type exports ──────────────────────────────────────────────────────────────

export type FlagType = z.infer<typeof FlagTypeSchema>;
export type FlagStatus = z.infer<typeof FlagStatusSchema>;
export type CriteriaType = z.infer<typeof CriteriaTypeSchema>;
export type CriteriaOperator = z.infer<typeof CriteriaOperatorSchema>;
export type FeatureFlagCriteria = z.infer<typeof FeatureFlagCriteriaSchema>;
export type FeatureFlag = z.infer<typeof FeatureFlagSchema>;
export type FeatureFlagPage = z.infer<typeof FeatureFlagPageSchema>;
export type CreateFeatureFlagResponse = z.infer<typeof CreateFeatureFlagResponseSchema>;
export type AddFeatureFlagCriteriaResponse = z.infer<typeof AddFeatureFlagCriteriaResponseSchema>;
