/**
 * system-suite.model.ts
 *
 * Re-exports types from the Zod schema (single source of truth).
 */
export type {
  SystemSuite,
  SystemSuitePage,
  SystemStatus,
  CreateSystemSuitePayload,
  CreateSystemSuiteResponse,
  SystemSuiteDomainResource,
  SystemSuiteCrudOperation,
  SystemSuiteCustomAction,
} from '../schemas/system-suite.schema';

export type {
  AddDomainResourceCommand,
  UpdateDomainResourceCommand,
} from '../schemas/system-suite.commands.schema';
