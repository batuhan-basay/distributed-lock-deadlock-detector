// High contention: many VUs hammer ONE hot resource through both API replicas.
// Shows lock wait time growing with load, while mutual exclusion and the acquisition timeout keep the system healthy.
//
//   k6 run loadtest/k6/contention.js
//   k6 run -e RESOURCE=inventory:sku-1 -e WORK_MS=10 loadtest/k6/contention.js
import http from 'k6/http';
import { check } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const APIS = (__ENV.APIS || 'http://localhost:8081,http://localhost:8082').split(',');
const RESOURCE = __ENV.RESOURCE || 'inventory:sku-42';
const WORK_MS = __ENV.WORK_MS || '20';
const WAIT_TIMEOUT_MS = __ENV.WAIT_TIMEOUT_MS || '10000';

const lockWait = new Trend('lockguard_lock_wait', true);
const lockTimeouts = new Rate('lockguard_lock_timeouts');

export const options = {
  scenarios: {
    ramp: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '20s', target: 10 },
        { duration: '40s', target: 40 },
        { duration: '20s', target: 0 },
      ],
      gracefulRampDown: '15s',
    },
  },
  thresholds: {
    lockguard_lock_timeouts: ['rate<0.01'],
    lockguard_lock_wait: ['p(95)<5000'],
    'checks{check:no server errors}': ['rate==1.0'],
  },
};

export default function () {
  const api = APIS[(__VU + __ITER) % APIS.length];
  const res = http.post(
    `${api}/api/test/contention?resource=${encodeURIComponent(RESOURCE)}&workMs=${WORK_MS}&waitTimeoutMs=${WAIT_TIMEOUT_MS}`,
    null,
    { tags: { name: 'contention' }, timeout: '30s' });

  check(res, {
    'no server errors': (r) => r.status < 500 && r.status !== 0,
    'acquired or timed out': (r) => r.status === 200 || r.status === 423,
  });

  lockTimeouts.add(res.status === 423);
  if (res.status === 200) {
    lockWait.add(res.json('waitedMs'));
  }
}
