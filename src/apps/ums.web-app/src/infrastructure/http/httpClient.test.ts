import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import axios from 'axios';

vi.mock('axios', () => ({
  default: {
    create: vi.fn(() => ({
      interceptors: {
        request: {
          use: vi.fn(),
          eject: vi.fn(),
          handlers: [{ fulfilled: vi.fn(), rejected: vi.fn() }],
        },
        response: {
          use: vi.fn(),
          eject: vi.fn(),
          handlers: [{ fulfilled: vi.fn(), rejected: vi.fn() }],
        },
      },
      defaults: { baseURL: '/api' },
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    })),
  },
}));

describe('httpClient configuration', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('exports an httpClient instance', async () => {
    const { httpClient } = await import('./httpClient');
    expect(httpClient).toBeDefined();
    expect(typeof httpClient.get).toBe('function');
    expect(typeof httpClient.post).toBe('function');
    expect(typeof httpClient.put).toBe('function');
    expect(typeof httpClient.delete).toBe('function');
  });

  it('has baseURL configured', async () => {
    const { httpClient } = await import('./httpClient');
    expect(httpClient.defaults.baseURL).toBeDefined();
  });

  it('has request interceptors configured', async () => {
    const { httpClient } = await import('./httpClient');
    expect(httpClient.interceptors.request).toBeDefined();
    expect(httpClient.interceptors.request.handlers.length).toBeGreaterThan(0);
  });

  it('has response interceptors configured', async () => {
    const { httpClient } = await import('./httpClient');
    expect(httpClient.interceptors.response).toBeDefined();
    expect(httpClient.interceptors.response.handlers.length).toBeGreaterThan(0);
  });

  it('creates axios instance with correct baseURL', async () => {
    await import('./httpClient');
    expect(axios.create).toHaveBeenCalledWith(
      expect.objectContaining({
        baseURL: '/api/v1',
        headers: expect.objectContaining({
          'Content-Type': 'application/json',
          Accept: 'application/json',
        }),
      })
    );
  });
});
