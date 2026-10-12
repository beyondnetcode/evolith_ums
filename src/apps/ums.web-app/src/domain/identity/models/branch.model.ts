/**
 * branch.model.ts — re-exports from Zod schema (single source of truth)
 */
export type {
  Branch,
  AddBranchPayload,
  AddBranchResponse,
  BranchLifecycleEntry,
  BranchState,
} from '../schemas/branch.schema';

export {
  branchState,
  canReactivateBranch,
  canDeactivateBranch,
  canCloseBranch,
} from '../schemas/branch.schema';
