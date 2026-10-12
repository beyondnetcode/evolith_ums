/**
 * UMS Smoke Test — verify API is up and critical endpoints respond.
 * Runs with 1 VU for 30 seconds. No auth required.
 *
 * Usage: k6 run --env BASE_URL=http://localhost:5000 smoke.js
 */
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  vus: 1,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],           // < 1% errors
    http_req_duration: ['p(95)<500'],          // 95th percentile < 500ms
  },
};

export default function () {
  // Liveness
  const live = http.get(`${BASE_URL}/health/live`);
  check(live, {
    'liveness 200': (r) => r.status === 200,
  });

  // Readiness
  const ready = http.get(`${BASE_URL}/health/ready`);
  check(ready, {
    'readiness 200 or 503': (r) => r.status === 200 || r.status === 503,
  });

  sleep(1);
}
