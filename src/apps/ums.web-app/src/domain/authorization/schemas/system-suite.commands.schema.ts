/**
 * system-suite.commands.schema.ts
 *
 * Zod schemas for REST command payloads (write DTOs).
 * These are separate from system-suite.schema.ts which holds read response shapes.
 */
import { z } from 'zod';

const codeRegex = /^[A-Za-z0-9_]+$/;

// ── SystemSuite ───────────────────────────────────────────────────────────────

export const CreateSystemSuiteCommandSchema = z.object({
  tenantId: z.string().uuid('TenantId must be a valid UUID'),
  code: z
    .string()
    .min(1, 'Código requerido')
    .max(50, 'Máximo 50 caracteres')
    .regex(codeRegex, 'Solo letras, dígitos y guiones bajos'),
  name: z.string().min(1, 'Nombre requerido').max(150, 'Máximo 150 caracteres'),
  description: z.string().min(1, 'Descripción requerida').max(500, 'Máximo 500 caracteres'),
});

export const UpdateSystemSuiteCommandSchema = z.object({
  name: z.string().min(1, 'Nombre requerido').max(150, 'Máximo 150 caracteres'),
  description: z.string().max(500, 'Máximo 500 caracteres').optional().default(''),
});

// ── Module ────────────────────────────────────────────────────────────────────

export const AddModuleCommandSchema = z.object({
  code: z
    .string()
    .min(1, 'Código requerido')
    .max(50, 'Máximo 50 caracteres')
    .regex(codeRegex, 'Solo letras, dígitos y guiones bajos'),
  name: z.string().min(1, 'Nombre requerido').max(150, 'Máximo 150 caracteres'),
  description: z.string().max(500, 'Máximo 500 caracteres').optional().default(''),
  sortOrder: z.number().int().positive('Debe ser un entero positivo'),
});

export const UpdateModuleCommandSchema = AddModuleCommandSchema;

// ── Action ────────────────────────────────────────────────────────────────────

export const RegisterActionCommandSchema = z.object({
  code: z
    .string()
    .min(1, 'Código requerido')
    .max(50, 'Máximo 50 caracteres')
    .regex(codeRegex, 'Solo letras, dígitos y guiones bajos'),
  name: z.string().min(1, 'Nombre requerido').max(150, 'Máximo 150 caracteres'),
});

// ── Domain Resources ──────────────────────────────────────────────────────────

export const AddDomainResourceCommandSchema = z
  .object({
    moduleId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/).nullable().optional(),
    parentResourceId: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/).nullable().optional(),
    type: z.enum(['Aggregate', 'Entity', 'DomainMethod']),
    code: z
      .string()
      .min(1, 'Código requerido')
      .max(100, 'Máximo 100 caracteres')
      .regex(codeRegex, 'Solo letras, dígitos y guiones bajos'),
    name: z.string().min(1, 'Nombre requerido').max(150, 'Máximo 150 caracteres'),
    description: z.string().min(1, 'Descripción requerida').max(500, 'Máximo 500 caracteres'),
  })
  .refine(data => data.type !== 'DomainMethod' || !!data.parentResourceId, {
    message: 'Un método de dominio debe pertenecer a un recurso padre.',
    path: ['parentResourceId'],
  });

export const UpdateDomainResourceCommandSchema = z.object({
  name: z.string().min(1, 'Nombre requerido').max(150, 'Máximo 150 caracteres'),
  description: z.string().min(1, 'Descripción requerida').max(500, 'Máximo 500 caracteres'),
});

// ── Inferred types ────────────────────────────────────────────────────────────

export type CreateSystemSuiteCommand = z.infer<typeof CreateSystemSuiteCommandSchema>;
export type UpdateSystemSuiteCommand = z.infer<typeof UpdateSystemSuiteCommandSchema>;
export type AddModuleCommand = z.infer<typeof AddModuleCommandSchema>;
export type UpdateModuleCommand = z.infer<typeof UpdateModuleCommandSchema>;
export type RegisterActionCommand = z.infer<typeof RegisterActionCommandSchema>;
export type AddDomainResourceCommand = z.infer<typeof AddDomainResourceCommandSchema>;
export type DomainResourceKind = 'Aggregate' | 'Entity' | 'DomainMethod';
export type UpdateDomainResourceCommand = z.infer<typeof UpdateDomainResourceCommandSchema>;
