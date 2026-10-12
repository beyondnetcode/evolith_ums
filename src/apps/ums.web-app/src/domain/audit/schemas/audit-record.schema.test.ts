import { describe, expect, it } from 'vitest';
import {
  AuditRecordSchema,
  AuditRecordPageSchema,
} from './audit-record.schema';

const validRecord = {
  auditRecordId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  whoActed: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
  subjectType: 'UserAccount',
  whenOccurred: '2024-01-01T00:00:00Z',
  whatChanged: 'status: Active -> Suspended',
  eventType: 'UserSuspended',
  auditResult: 'Success',
  affectedEntityId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
  affectedEntityType: 'UserAccount',
  rootTenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
  metadata: '{"reason":"policy"}',
};

describe('AuditRecordSchema', () => {
  it('acepta un registro de auditoría válido', () => {
    const record = AuditRecordSchema.parse(validRecord);
    expect(record.eventType).toBe('UserSuspended');
    expect(record.auditResult).toBe('Success');
    expect(record.metadata).toBe('{"reason":"policy"}');
  });

  it('acepta metadata nula', () => {
    const record = AuditRecordSchema.parse({ ...validRecord, metadata: null });
    expect(record.metadata).toBeNull();
  });

  it('acepta metadata omitida (opcional)', () => {
    const { metadata: _omitted, ...withoutMetadata } = validRecord;
    const record = AuditRecordSchema.parse(withoutMetadata);
    expect(record.metadata).toBeUndefined();
  });

  it('rechaza auditRecordId no-UUID', () => {
    expect(() =>
      AuditRecordSchema.parse({ ...validRecord, auditRecordId: 'no-es-uuid' })
    ).toThrow();
  });

  it('rechaza whoActed no-UUID', () => {
    expect(() => AuditRecordSchema.parse({ ...validRecord, whoActed: '123' })).toThrow();
  });

  it('rechaza affectedEntityId no-UUID', () => {
    expect(() =>
      AuditRecordSchema.parse({ ...validRecord, affectedEntityId: 'x' })
    ).toThrow();
  });

  it('rechaza rootTenantId no-UUID', () => {
    expect(() => AuditRecordSchema.parse({ ...validRecord, rootTenantId: 'x' })).toThrow();
  });

  it('rechaza subjectType vacío', () => {
    expect(() => AuditRecordSchema.parse({ ...validRecord, subjectType: '' })).toThrow();
  });

  it('rechaza whenOccurred vacío', () => {
    expect(() => AuditRecordSchema.parse({ ...validRecord, whenOccurred: '' })).toThrow();
  });

  it('rechaza whatChanged vacío', () => {
    expect(() => AuditRecordSchema.parse({ ...validRecord, whatChanged: '' })).toThrow();
  });

  it('rechaza eventType vacío', () => {
    expect(() => AuditRecordSchema.parse({ ...validRecord, eventType: '' })).toThrow();
  });

  it('rechaza auditResult vacío', () => {
    expect(() => AuditRecordSchema.parse({ ...validRecord, auditResult: '' })).toThrow();
  });

  it('rechaza affectedEntityType vacío', () => {
    expect(() =>
      AuditRecordSchema.parse({ ...validRecord, affectedEntityType: '' })
    ).toThrow();
  });
});

describe('AuditRecordPageSchema', () => {
  const validPage = {
    items: [validRecord],
    page: 1,
    pageSize: 20,
    totalItems: 1,
    totalPages: 1,
  };

  it('acepta una página válida con items', () => {
    const page = AuditRecordPageSchema.parse(validPage);
    expect(page.items).toHaveLength(1);
    expect(page.page).toBe(1);
    expect(page.pageSize).toBe(20);
  });

  it('acepta una página vacía con totales en cero', () => {
    const page = AuditRecordPageSchema.parse({
      items: [],
      page: 1,
      pageSize: 20,
      totalItems: 0,
      totalPages: 0,
    });
    expect(page.items).toHaveLength(0);
    expect(page.totalItems).toBe(0);
    expect(page.totalPages).toBe(0);
  });

  it('rechaza page menor que 1', () => {
    expect(() => AuditRecordPageSchema.parse({ ...validPage, page: 0 })).toThrow();
  });

  it('rechaza pageSize menor que 1', () => {
    expect(() => AuditRecordPageSchema.parse({ ...validPage, pageSize: 0 })).toThrow();
  });

  it('rechaza totalItems negativo', () => {
    expect(() => AuditRecordPageSchema.parse({ ...validPage, totalItems: -1 })).toThrow();
  });

  it('rechaza totalPages negativo', () => {
    expect(() => AuditRecordPageSchema.parse({ ...validPage, totalPages: -1 })).toThrow();
  });

  it('rechaza page no entero', () => {
    expect(() => AuditRecordPageSchema.parse({ ...validPage, page: 1.5 })).toThrow();
  });

  it('rechaza un item con forma inválida dentro de la página', () => {
    expect(() =>
      AuditRecordPageSchema.parse({
        ...validPage,
        items: [{ ...validRecord, auditRecordId: 'invalido' }],
      })
    ).toThrow();
  });
});
