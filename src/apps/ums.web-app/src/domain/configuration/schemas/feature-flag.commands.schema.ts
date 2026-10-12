/**
 * feature-flag.commands.schema.ts — Command payload schemas (Zod)
 *
 * Validate outbound command payloads before they reach the API.
 */
import { z } from 'zod';
import { FlagTypeSchema, CriteriaTypeSchema, CriteriaOperatorSchema } from './feature-flag.schema';

export const CreateFeatureFlagPayloadSchema = z.object({
  systemSuiteId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  tenantId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/).nullable().optional(),
  flagCode: z
    .string()
    .min(1)
    .max(100)
    .regex(/^[A-Z0-9_]+$/, 'Only uppercase letters, digits and underscores'),
  flagType: FlagTypeSchema,
  flagTargets: z.string().default('*'),
  rolloutPercentage: z.number().int().min(0).max(100).nullable().optional(),
});

export const UpdateFeatureFlagPayloadSchema = z.object({
  flagTargets: z.string().min(1),
  rolloutPercentage: z.number().int().min(0).max(100).nullable().optional(),
});

export const AddFeatureFlagCriteriaPayloadSchema = z.object({
  criteriaType: CriteriaTypeSchema,
  operator: CriteriaOperatorSchema,
  value: z.string().min(1),
});

export type CreateFeatureFlagPayload = z.infer<typeof CreateFeatureFlagPayloadSchema>;
export type UpdateFeatureFlagPayload = z.infer<typeof UpdateFeatureFlagPayloadSchema>;
export type AddFeatureFlagCriteriaPayload = z.infer<typeof AddFeatureFlagCriteriaPayloadSchema>;
