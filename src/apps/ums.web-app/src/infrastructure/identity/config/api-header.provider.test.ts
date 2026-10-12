import { describe, it, expect, beforeEach } from 'vitest';
import { setApiHeaderProvider, getApiHeaders } from './api-header.provider';

describe('api-header.provider', () => {
  beforeEach(() => {
    setApiHeaderProvider(null as any);
  });

  it('returns empty object when no provider is set', () => {
    expect(getApiHeaders()).toEqual({});
  });

  it('returns headers from provider when set', () => {
    const mockProvider = {
      getHeaders: () => ({ 'X-Custom-Header': 'test-value' }),
    };

    setApiHeaderProvider(mockProvider);

    expect(getApiHeaders()).toEqual({ 'X-Custom-Header': 'test-value' });
  });
});
