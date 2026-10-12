/**
 * feature-flag.model.ts
 *
 * Re-exports types from the Zod schema (single source of truth).
 */
export type {
  FlagType,
  FlagStatus,
  CriteriaType,
  CriteriaOperator,
  FeatureFlagCriteria,
  FeatureFlag,
  FeatureFlagPage,
  CreateFeatureFlagResponse,
  AddFeatureFlagCriteriaResponse,
} from '../schemas/feature-flag.schema';

export type {
  CreateFeatureFlagPayload,
  UpdateFeatureFlagPayload,
  AddFeatureFlagCriteriaPayload,
} from '../schemas/feature-flag.commands.schema';
