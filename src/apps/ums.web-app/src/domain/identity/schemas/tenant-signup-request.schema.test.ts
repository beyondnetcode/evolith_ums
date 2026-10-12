import { describe, expect, it } from 'vitest';
import {
  TenantSignupRequestSchema,
  TenantSignupRequestListSchema,
  ApproveTenantSignupResponseSchema,
} from './tenant-signup-request.schema';

const validRequest = {
  tenantSignupRequestId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  companyName: 'Naviera del Pacífico S.A.',
  companyReference: 'REF-2024-001',
  contactName: 'Ana Torres',
  contactEmail: 'ana.torres@naviera.com.pe',
  status: 'Pending',
  approvedTenantId: null,
  requestedAtUtc: '2024-01-01T00:00:00Z',
};

describe('TenantSignupRequestSchema', () => {
  it('acepta una solicitud de alta de inquilino válida', () => {
    const parsed = TenantSignupRequestSchema.parse(validRequest);
    expect(parsed.companyName).toBe('Naviera del Pacífico S.A.');
    expect(parsed.status).toBe('Pending');
  });

  it('transforma approvedTenantId omitido a null', () => {
    const withoutApproved: Record<string, unknown> = { ...validRequest };
    delete withoutApproved.approvedTenantId;
    const parsed = TenantSignupRequestSchema.parse(withoutApproved);
    expect(parsed.approvedTenantId).toBeNull();
  });

  it('acepta approvedTenantId como UUID válido', () => {
    const parsed = TenantSignupRequestSchema.parse({
      ...validRequest,
      approvedTenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
    });
    expect(parsed.approvedTenantId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa7');
  });

  it('rechaza companyName vacío', () => {
    expect(() => TenantSignupRequestSchema.parse({ ...validRequest, companyName: '' })).toThrow();
  });

  it('rechaza contactEmail inválido', () => {
    expect(() =>
      TenantSignupRequestSchema.parse({ ...validRequest, contactEmail: 'no-email' })
    ).toThrow();
  });

  it('rechaza requestedAtUtc que no es datetime ISO', () => {
    expect(() =>
      TenantSignupRequestSchema.parse({ ...validRequest, requestedAtUtc: '2024-01-01' })
    ).toThrow();
  });

  it('rechaza tenantSignupRequestId con GUID malformado', () => {
    expect(() =>
      TenantSignupRequestSchema.parse({ ...validRequest, tenantSignupRequestId: '123' })
    ).toThrow();
  });
});

describe('TenantSignupRequestListSchema', () => {
  it('acepta un arreglo de solicitudes', () => {
    const parsed = TenantSignupRequestListSchema.parse([validRequest]);
    expect(parsed).toHaveLength(1);
  });
});

describe('ApproveTenantSignupResponseSchema', () => {
  const validResponse = {
    tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    userAccountId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
    temporaryPassword: 'Temp!2024',
    message: 'Inquilino aprobado correctamente',
  };

  it('acepta una respuesta de aprobación válida', () => {
    const parsed = ApproveTenantSignupResponseSchema.parse(validResponse);
    expect(parsed.temporaryPassword).toBe('Temp!2024');
  });

  it('rechaza temporaryPassword vacío', () => {
    expect(() =>
      ApproveTenantSignupResponseSchema.parse({ ...validResponse, temporaryPassword: '' })
    ).toThrow();
  });

  it('rechaza tenantId no-UUID', () => {
    expect(() =>
      ApproveTenantSignupResponseSchema.parse({ ...validResponse, tenantId: 'x' })
    ).toThrow();
  });
});
