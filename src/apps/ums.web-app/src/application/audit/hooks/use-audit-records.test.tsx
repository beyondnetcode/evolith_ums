import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import { useGetAuditRecords } from './use-audit-records';
import { auditRecordService } from '@infra/audit/services/audit-record.service';

vi.mock('@infra/audit/services/audit-record.service', () => ({
  auditRecordService: {
    getAll: vi.fn(),
  },
  default: {
    getAll: vi.fn(),
  },
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

const mockPage = {
  items: [
    {
      auditRecordId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      whoActed: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
      subjectType: 'UserAccount',
      whenOccurred: '2024-01-01T00:00:00Z',
      whatChanged: 'status changed',
      eventType: 'UserSuspended',
      auditResult: 'Success',
      affectedEntityId: '3fa85f64-5717-4562-b3fc-2c963f66afa8',
      affectedEntityType: 'UserAccount',
      rootTenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa9',
      metadata: null,
    },
  ],
  page: 1,
  pageSize: 20,
  totalItems: 1,
  totalPages: 1,
};

describe('useGetAuditRecords', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('devuelve la página de registros en estado de éxito', async () => {
    vi.mocked(auditRecordService.getAll).mockResolvedValue(mockPage);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetAuditRecords({ page: 1, pageSize: 20 }), {
      wrapper,
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.items[0].eventType).toBe('UserSuspended');
    expect(result.current.data?.totalItems).toBe(1);
    expect(auditRecordService.getAll).toHaveBeenCalledWith({ page: 1, pageSize: 20 });
  });

  it('propaga los parámetros de filtro al servicio', async () => {
    vi.mocked(auditRecordService.getAll).mockResolvedValue(mockPage);

    const params = {
      page: 2,
      pageSize: 10,
      eventType: 'RoleAssigned',
      actorId: '3fa85f64-5717-4562-b3fc-2c963f66afa7',
    };
    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetAuditRecords(params), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(auditRecordService.getAll).toHaveBeenCalledWith(params);
  });

  it('no invoca al servicio cuando enabled es false', async () => {
    vi.mocked(auditRecordService.getAll).mockResolvedValue(mockPage);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetAuditRecords(undefined, false), { wrapper });

    expect(result.current.fetchStatus).toBe('idle');
    expect(result.current.data).toBeUndefined();
    expect(auditRecordService.getAll).not.toHaveBeenCalled();
  });

  it('expone el estado de error cuando el servicio rechaza', async () => {
    vi.mocked(auditRecordService.getAll).mockRejectedValue(new Error('boom'));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useGetAuditRecords(), { wrapper });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    expect(result.current.error?.message).toBe('boom');
  });
});
