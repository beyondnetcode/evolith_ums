import axios, { type AxiosInstance } from 'axios';
import { BASE_URL } from './request-context';

/**
 * Public HTTP client for anonymous endpoints.
 *
 * Intentionally omits tenant and auth headers so public signup flows do not
 * trigger tenant-scoped backend filters.
 */
function createPublicClient(): AxiosInstance {
  return axios.create({
    baseURL: BASE_URL,
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
    withCredentials: false,
  });
}

export const publicClient = createPublicClient();
