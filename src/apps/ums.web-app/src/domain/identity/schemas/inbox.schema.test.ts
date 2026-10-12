import { describe, expect, it } from 'vitest';
import {
  PendingUserSignupSchema,
  PendingProfileRequestSchema,
  PendingUserSignupListSchema,
  PendingProfileRequestListSchema,
} from './inbox.schema';

const validSignup = {
  userAccountId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
  email: 'nuevo@evolith.example',
  displayName: 'Nuevo Usuario',
  category: 'Standard',
  requestedAt: '2024-01-01T00:00:00Z',
};

const validProfileRequest = {
  approvalRequestId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  targetUserId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
  requestedSystemId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
  requestedBranchId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
  requestedRoleId: '3fa85f64-5717-4562-b3fc-2c963f66afaa',
  justification: 'Requiere acceso al módulo de despacho',
  requestedAt: '2024-01-01T00:00:00Z',
};

describe('PendingUserSignupSchema', () => {
  it('acepta un alta pendiente válida', () => {
    const parsed = PendingUserSignupSchema.parse(validSignup);
    expect(parsed.email).toBe('nuevo@evolith.example');
    expect(parsed.displayName).toBe('Nuevo Usuario');
  });

  it('transforma displayName nulo a null', () => {
    const parsed = PendingUserSignupSchema.parse({ ...validSignup, displayName: null });
    expect(parsed.displayName).toBeNull();
  });

  it('transforma displayName omitido a null', () => {
    const withoutDisplayName: Record<string, unknown> = { ...validSignup };
    delete withoutDisplayName.displayName;
    const parsed = PendingUserSignupSchema.parse(withoutDisplayName);
    expect(parsed.displayName).toBeNull();
  });

  it('rechaza email inválido', () => {
    expect(() => PendingUserSignupSchema.parse({ ...validSignup, email: 'no-es-email' })).toThrow();
  });

  it('rechaza userAccountId no-UUID', () => {
    expect(() => PendingUserSignupSchema.parse({ ...validSignup, userAccountId: 'x' })).toThrow();
  });
});

describe('PendingProfileRequestSchema', () => {
  it('acepta una solicitud de perfil válida', () => {
    const parsed = PendingProfileRequestSchema.parse(validProfileRequest);
    expect(parsed.justification).toBe('Requiere acceso al módulo de despacho');
  });

  it('transforma requestedBranchId nulo a null', () => {
    const parsed = PendingProfileRequestSchema.parse({
      ...validProfileRequest,
      requestedBranchId: null,
    });
    expect(parsed.requestedBranchId).toBeNull();
  });

  it('transforma justification omitida a null', () => {
    const withoutJustification: Record<string, unknown> = { ...validProfileRequest };
    delete withoutJustification.justification;
    const parsed = PendingProfileRequestSchema.parse(withoutJustification);
    expect(parsed.justification).toBeNull();
  });

  it('rechaza requestedRoleId no-UUID', () => {
    expect(() =>
      PendingProfileRequestSchema.parse({ ...validProfileRequest, requestedRoleId: 'x' })
    ).toThrow();
  });
});

describe('listas de bandeja', () => {
  it('PendingUserSignupListSchema acepta un arreglo de altas', () => {
    const parsed = PendingUserSignupListSchema.parse([validSignup, validSignup]);
    expect(parsed).toHaveLength(2);
  });

  it('PendingProfileRequestListSchema acepta un arreglo vacío', () => {
    const parsed = PendingProfileRequestListSchema.parse([]);
    expect(parsed).toHaveLength(0);
  });
});
