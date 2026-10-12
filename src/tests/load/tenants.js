/**
 * UMS Tenant API Load Test — realistic read/write traffic mix.
 *
 * Simulates:
 *   70% paginated tenant list (GET /api/v1/tenants)
 *   20% single tenant fetch (GET /api/v1/tenants/{id})
 *   10% tenant creation (POST /api/v1/tenants)
 *
 * Usage:
 *   k6 run --env BASE_URL=http://localhost:5000 \
 *          --env TOKEN=<jwt_or_dev_user_id> \
 *          tenants.js
 */
import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const BASE_URL  = __ENV.BASE_URL  || 'http://localhost:5000';
const TOKEN     = __ENV.TOKEN     || 'dev-user';
const API_BASE  = `${BASE_URL}/api/v1`;

// Custom metrics
const createErrors = new Rate('tenant_create_errors');
const listLatency  = new Trend('tenant_list_latency', true);

export const options = {
  scenarios: {
    // Ramp up to 50 VUs, hold for 3 minutes, ramp down
    load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 10  },
        { duration: '3m',  target: 50  },
        { duration: '30s', target: 0   },
      ],
    },
  },
  thresholds: {
    http_req_failed:       ['rate<0.01'],
    http_req_duration:     ['p(95)<500', 'p(99)<1500'],
    tenant_list_latency:   ['p(95)<400'],
    tenant_create_errors:  ['rate<0.02'],
  },
};

const HEADERS = {
  'Content-Type': 'application/json',
  'X-User-Id':    TOKEN,          // dev auth header (replace with Authorization: Bearer in prod)
  'X-Idempotency-Key': `${Date.now()}-${Math.random()}`,
};

// Shared state (populated during test)
let knownTenantId = null;

export function setup() {
  // Pre-create one tenant to use in read operations
  const body = JSON.stringify({
    code: `LOAD_TEST_${Date.now()}`,
    name: 'Load Test Tenant',
    organizationTypeId: 1,
    idpStrategyId: 1,
  });

  const res = http.post(`${API_BASE}/tenants`, body, { headers: HEADERS });
  if (res.status === 201) {
    const data = res.json();
    return { tenantId: data?.tenantId };
  }
  return { tenantId: null };
}

export default function (data) {
  const tenantId = data?.tenantId || knownTenantId;
  const roll     = Math.random();

  if (roll < 0.70) {
    // 70% — paginated list
    group('List tenants', () => {
      const start = Date.now();
      const res = http.get(
        `${API_BASE}/tenants?page=1&pageSize=20&sortBy=name&sortOrder=asc`,
        { headers: HEADERS },
      );
      listLatency.add(Date.now() - start);
      check(res, {
        'list status 200': (r) => r.status === 200,
        'list has items':  (r) => Array.isArray(r.json('items')),
        'list has total':  (r) => r.json('totalCount') >= 0,
      });
    });
  } else if (roll < 0.90 && tenantId) {
    // 20% — single tenant
    group('Get tenant by id', () => {
      const res = http.get(`${API_BASE}/tenants/${tenantId}`, { headers: HEADERS });
      check(res, {
        'get status 200': (r) => r.status === 200,
        'get has code':   (r) => !!r.json('code'),
      });
    });
  } else {
    // 10% — create (may 409 if code already exists, that is acceptable)
    group('Create tenant', () => {
      const body = JSON.stringify({
        code: `TEN_${Date.now()}_${__VU}`,
        name: `VU ${__VU} Tenant`,
        organizationTypeId: 1,
        idpStrategyId: 1,
      });
      const headers = { ...HEADERS, 'X-Idempotency-Key': `vu-${__VU}-${Date.now()}` };
      const res = http.post(`${API_BASE}/tenants`, body, { headers });
      const ok  = res.status === 201 || res.status === 409;
      createErrors.add(!ok);
      check(res, { 'create 201 or 409': () => ok });
    });
  }

  sleep(Math.random() * 0.5 + 0.1); // 100–600 ms think time
}
