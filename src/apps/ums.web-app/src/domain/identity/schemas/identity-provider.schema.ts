/**
 * identity-provider.schema.ts
 *
 * Zod schemas for Identity Provider runtime validation.
 */
import { z } from 'zod';

export const IdentityProviderSchema = z.object({
  identityProviderId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/),
  code: z.string().min(1),
  name: z.string().min(1),
  description: z.string().default(''),
  strategy: z.string(),
  isActive: z.boolean(),
});

export const IdentityProviderListSchema = z.array(IdentityProviderSchema);

export type IdentityProvider = z.infer<typeof IdentityProviderSchema>;

export const RegisterIdentityProviderPayloadSchema = z.object({
  code: z.string().min(1),
  name: z.string().min(1),
  description: z.string().optional(),
  strategy: z.string().min(1),
});

export type RegisterIdentityProviderPayload = z.infer<typeof RegisterIdentityProviderPayloadSchema>;
