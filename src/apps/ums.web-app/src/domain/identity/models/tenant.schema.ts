import { z } from 'zod';

export const TenantSchema = z.object({
  tenantId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  code: z.string().min(1).max(50),
  name: z.string().min(1).max(200),
  type: z.enum(['INTERNAL', 'SUPPLIER', 'CLIENT']),
  status: z.enum(['Active', 'Suspended', 'Pending']),
  companyReference: z.string().max(100).optional().nullable(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

export const CreateTenantResponseSchema = z.object({
  tenantId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  code: z.string().min(1).max(50),
  name: z.string().min(1).max(200),
});

export const BranchSchema = z.object({
  branchId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  code: z.string().min(1).max(50),
  name: z.string().min(1).max(200),
  isActive: z.boolean(),
  geofencingMetadata: z.string().max(500).optional().nullable(),
});

export const AddBranchResponseSchema = z.object({
  branchId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  code: z.string().min(1).max(50),
});

export function validateTenants(data: unknown): z.infer<typeof TenantSchema>[] {
  return z.array(TenantSchema).parse(data);
}

export function validateTenant(data: unknown): z.infer<typeof TenantSchema> {
  return TenantSchema.parse(data);
}

export function validateCreateTenantResponse(
  data: unknown
): z.infer<typeof CreateTenantResponseSchema> {
  return CreateTenantResponseSchema.parse(data);
}

export function validateBranches(data: unknown): z.infer<typeof BranchSchema>[] {
  return z.array(BranchSchema).parse(data);
}

export function validateAddBranchResponse(data: unknown): z.infer<typeof AddBranchResponseSchema> {
  return AddBranchResponseSchema.parse(data);
}
