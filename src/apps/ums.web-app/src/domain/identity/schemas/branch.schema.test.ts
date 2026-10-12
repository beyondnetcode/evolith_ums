import { describe, expect, it } from 'vitest';
import {
  BranchSchema,
  BranchListSchema,
  BranchLifecycleSchema,
  AddBranchPayloadSchema,
  AddBranchResponseSchema,
  branchState,
  canReactivateBranch,
  canDeactivateBranch,
  canCloseBranch,
} from './branch.schema';

describe('BranchSchema', () => {
  it('accepts a valid branch payload', () => {
    const branch = BranchSchema.parse({
      branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'BR001',
      name: 'Main Branch',
      isActive: true,
      geofencingMetadata: null,
    });

    expect(branch.code).toBe('BR001');
    expect(branch.name).toBe('Main Branch');
    expect(branch.isActive).toBe(true);
  });

  it('accepts optional geofencingMetadata', () => {
    const branch = BranchSchema.parse({
      branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'BR002',
      name: 'Warehouse',
      isActive: false,
      geofencingMetadata: '{"lat": -12.0464, "lng": -77.0428}',
    });

    expect(branch.geofencingMetadata).toBe('{"lat": -12.0464, "lng": -77.0428}');
  });

  it('transforms undefined geofencingMetadata to null', () => {
    const branch = BranchSchema.parse({
      branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'BR003',
      name: 'Office',
      isActive: true,
    });

    expect(branch.geofencingMetadata).toBeNull();
  });

  it('rejects invalid UUID for branchId', () => {
    expect(() =>
      BranchSchema.parse({
        branchId: 'not-a-uuid',
        code: 'BR001',
        name: 'Main Branch',
        isActive: true,
      })
    ).toThrow();
  });

  it('rejects empty code', () => {
    expect(() =>
      BranchSchema.parse({
        branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        code: '',
        name: 'Main Branch',
        isActive: true,
      })
    ).toThrow();
  });

  it('rejects empty name', () => {
    expect(() =>
      BranchSchema.parse({
        branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        code: 'BR001',
        name: '',
        isActive: true,
      })
    ).toThrow();
  });
});

// ─── ADR-0164: cierre definitivo frente a desactivación reversible ───────────

describe('BranchSchema · cierre definitivo', () => {
  const closed = {
    branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    code: 'BR009',
    name: 'Sucursal Callao',
    isActive: false,
    isClosed: true,
    closedAtUtc: '2026-07-31T15:04:05Z',
  };

  it('conserva la marca y la fecha de cierre', () => {
    const branch = BranchSchema.parse(closed);

    expect(branch.isClosed).toBe(true);
    expect(branch.closedAtUtc).toBe('2026-07-31T15:04:05Z');
  });

  it('trata como abierta la sucursal que llega sin la marca', () => {
    const branch = BranchSchema.parse({
      branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      code: 'BR010',
      name: 'Sucursal Lima',
      isActive: true,
    });

    expect(branch.isClosed).toBe(false);
    expect(branch.closedAtUtc).toBeNull();
  });
});

describe('branchState', () => {
  it('distingue activa, pausada y cerrada', () => {
    expect(branchState({ isActive: true, isClosed: false })).toBe('Active');
    expect(branchState({ isActive: false, isClosed: false })).toBe('Inactive');
    expect(branchState({ isActive: false, isClosed: true })).toBe('Closed');
  });

  it('el cierre manda sobre la actividad: una cerrada nunca se pinta activa', () => {
    expect(branchState({ isActive: true, isClosed: true })).toBe('Closed');
  });
});

describe('transiciones permitidas', () => {
  const activa = { isActive: true, isClosed: false };
  const pausada = { isActive: false, isClosed: false };
  const cerrada = { isActive: false, isClosed: true };

  it('solo la pausada se puede reactivar: el cierre es irreversible', () => {
    expect(canReactivateBranch(pausada)).toBe(true);
    expect(canReactivateBranch(activa)).toBe(false);
    expect(canReactivateBranch(cerrada)).toBe(false);
  });

  it('solo la activa se puede desactivar', () => {
    expect(canDeactivateBranch(activa)).toBe(true);
    expect(canDeactivateBranch(pausada)).toBe(false);
    expect(canDeactivateBranch(cerrada)).toBe(false);
  });

  it('lo ya cerrado no se vuelve a cerrar', () => {
    expect(canCloseBranch(activa)).toBe(true);
    expect(canCloseBranch(pausada)).toBe(true);
    expect(canCloseBranch(cerrada)).toBe(false);
  });
});

describe('BranchLifecycleSchema', () => {
  it('acepta los asientos de la bitácora', () => {
    const entries = BranchLifecycleSchema.parse([
      {
        entryId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        episode: 'Opened',
        occurredAtUtc: '2026-01-02T10:00:00Z',
        actorId: 'admin@evolith.example',
        nameSnapshot: 'Sucursal Callao',
        geofencingSnapshot: '-12.0464,-77.0428;r=800',
        reason: null,
      },
    ]);

    expect(entries[0].episode).toBe('Opened');
    expect(entries[0].reason).toBeNull();
  });

  // Si el backend añade un episodio, la bitácora debe seguir viéndose: es justo cuando se audita.
  it('no rechaza un episodio que todavía no conoce', () => {
    const entries = BranchLifecycleSchema.parse([
      {
        entryId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        episode: 'Relocated',
        occurredAtUtc: '2026-03-02T10:00:00Z',
        actorId: 'admin@evolith.example',
        nameSnapshot: 'Sucursal Callao',
      },
    ]);

    expect(entries[0].episode).toBe('Relocated');
    expect(entries[0].geofencingSnapshot).toBeNull();
  });
});

describe('BranchListSchema', () => {
  it('accepts an array of valid branches', () => {
    const branches = BranchListSchema.parse([
      {
        branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        code: 'BR001',
        name: 'Main Branch',
        isActive: true,
        geofencingMetadata: null,
      },
      {
        branchId: '4fa85f64-5717-4562-b3fc-2c963f66afa7',
        code: 'BR002',
        name: 'Secondary Branch',
        isActive: false,
      },
    ]);

    expect(branches).toHaveLength(2);
    expect(branches[0].code).toBe('BR001');
  });
});

describe('AddBranchPayloadSchema', () => {
  it('accepts a valid add branch payload', () => {
    const payload = AddBranchPayloadSchema.parse({
      code: 'NEW001',
      name: 'New Branch',
    });

    expect(payload.code).toBe('NEW001');
    expect(payload.name).toBe('New Branch');
  });

  it('accepts optional geofencingMetadata', () => {
    const payload = AddBranchPayloadSchema.parse({
      code: 'NEW002',
      name: 'Geo Branch',
      geofencingMetadata: '{"radius": 500}',
    });

    expect(payload.geofencingMetadata).toBe('{"radius": 500}');
  });

  it('rejects code exceeding max length', () => {
    expect(() =>
      AddBranchPayloadSchema.parse({
        code: 'A'.repeat(21),
        name: 'Too Long Code',
      })
    ).toThrow();
  });

  it('rejects name exceeding max length', () => {
    expect(() =>
      AddBranchPayloadSchema.parse({
        code: 'X01',
        name: 'A'.repeat(121),
      })
    ).toThrow();
  });
});

describe('AddBranchResponseSchema', () => {
  it('accepts a valid response', () => {
    const response = AddBranchResponseSchema.parse({
      branchId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      tenantId: 'c9b736b4-6a84-48f8-b34d-176bc5a6d542',
      code: 'NEW001',
    });

    expect(response.branchId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
  });

  it('rejects non-UUID branchId', () => {
    expect(() =>
      AddBranchResponseSchema.parse({
        branchId: 'invalid',
        tenantId: 'c9b736b4-6a84-48f8-b34d-176bc5a6d542',
        code: 'NEW001',
      })
    ).toThrow();
  });
});
