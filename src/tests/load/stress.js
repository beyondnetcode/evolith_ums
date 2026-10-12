/**
 * UMS Stress Test — find the API's breaking point.
 * Ramps VUs exponentially until error rate exceeds threshold or VU cap is reached.
 *
 * Usage: k6 run --env BASE_URL=http://localhost:5000 stress.js
 */
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const TOKEN    = __ENV.TOKEN    || 'dev-user';

export const options = {
  scenarios: {
    stress: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '1m',  target: 50  },
        { duration: '2m',  target: 100 },
        { duration: '2m',  target: 200 },
        { duration: '2m',  target: 300 },
        { duration: '30s', target: 0   },
      ],
    },
  },
  thresholds: {
    http_req_failed:   ['rate<0.10'],   // break at 10% errors
    http_req_duration: ['p(99)<3000'],  // break at 3 s p99
  },
};

const HEADERS = {
  'Content-Type': 'application/json',
  'X-User-Id':    TOKEN,
};

export default function () {
  const res = http.get(
    `${BASE_URL}/api/v1/tenants?page=1&pageSize=10`,
    { headers: HEADERS },
  );
  check(res, { 'status 200': (r) => r.status === 200 });
  sleep(0.1);
}
