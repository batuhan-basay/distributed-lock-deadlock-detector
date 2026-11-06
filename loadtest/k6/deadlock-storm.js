// Deadlock storm: every iteration fires scenario A on api-1 and scenario B on api-2 at the same instant,
// i.e. two sessions on two different processes take account:A / account:B in opposite order.
// Every pair MUST end with exactly one winner; the detector picks the victim (HTTP 409) unless MAX_RETRIES > 0,
// in which case the victim retries the unit of work and both requests complete.
//
//   k6 run loadtest/k6/deadlock-storm.js
//   k6 run -e MAX_RETRIES=2 -e PAIRS=5 -e DURATION=2m loadtest/k6/deadlock-storm.js
//   docker run --rm -i --network host grafana/k6 run - < loadtest/k6/deadlock-storm.js
import http from 'k6/http';
import { check } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

const API_1 = __ENV.API_1 || 'http://localhost:8081';
const API_2 = __ENV.API_2 || 'http://localhost:8082';
const HOLD_MS = __ENV.HOLD_MS || '800';
const MAX_RETRIES = __ENV.MAX_RETRIES || '0';

const deadlockVictims = new Counter('lockguard_deadlock_victims');
const completedTransfers = new Counter('lockguard_completed_transfers');
const pairResolved = new Rate('lockguard_pair_resolved');           // at least one side of every pair completed
const pairBothCompleted = new Rate('lockguard_pair_both_completed'); // only expected with MAX_RETRIES > 0
const timeToResolve = new Trend('lockguard_time_to_resolve', true);  // duration of the slower request of the pair

export const options = {
  scenarios: {
    storm: {
      executor: 'constant-vus',
      vus: Number(__ENV.PAIRS || 1),
      duration: __ENV.DURATION || '1m',
    },
  },
  thresholds: {
    // With PAIRS=1 every pair has exactly one winner. With PAIRS>1 combine with MAX_RETRIES>0 (victims retry).
    lockguard_pair_resolved: ['rate==1.0'],
    lockguard_time_to_resolve: ['p(95)<10000'],
    http_req_failed: ['rate<0.60'],                      // 409 victims count as "failed" for k6
    'checks{check:no server errors}': ['rate==1.0'],
  },
};

export default function () {
  // All VUs share account:A / account:B on purpose: with PAIRS > 1, cycles of more than two sessions and
  // pure contention mix with the two-party deadlocks, which is exactly what the detector has to untangle.
  const query = `holdMs=${HOLD_MS}&maxRetries=${MAX_RETRIES}`;

  const responses = http.batch([
    ['POST', `${API_1}/api/test/deadlock-scenario-a?${query}`, null, { tags: { name: 'scenario-a' }, timeout: '60s' }],
    ['POST', `${API_2}/api/test/deadlock-scenario-b?${query}`, null, { tags: { name: 'scenario-b' }, timeout: '60s' }],
  ]);

  let completed = 0;
  for (const res of responses) {
    check(res, {
      'no server errors': (r) => r.status < 500 && r.status !== 0,
      'completed or victim': (r) => r.status === 200 || r.status === 409,
    });
    if (res.status === 200) {
      completed++;
      completedTransfers.add(1);
    } else if (res.status === 409) {
      deadlockVictims.add(1);
    }
  }

  pairResolved.add(completed >= 1);
  pairBothCompleted.add(completed === 2);
  timeToResolve.add(Math.max(responses[0].timings.duration, responses[1].timings.duration));
}

export function handleSummary(data) {
  const m = data.metrics;
  const val = (name, key) => (m[name] && m[name].values[key] !== undefined ? m[name].values[key] : 0);
  const report = {
    pairs: val('lockguard_pair_resolved', 'passes') + val('lockguard_pair_resolved', 'fails'),
    pairsResolved: val('lockguard_pair_resolved', 'rate'),
    pairsBothCompleted: val('lockguard_pair_both_completed', 'rate'),
    deadlockVictims: val('lockguard_deadlock_victims', 'count'),
    completedTransfers: val('lockguard_completed_transfers', 'count'),
    timeToResolveMs: {
      avg: val('lockguard_time_to_resolve', 'avg'),
      p95: val('lockguard_time_to_resolve', 'p(95)'),
      max: val('lockguard_time_to_resolve', 'max'),
    },
  };
  return {
    stdout: `\n=== LockGuard deadlock storm report ===\n${JSON.stringify(report, null, 2)}\n`,
    'deadlock-storm-summary.json': JSON.stringify(data, null, 2),
  };
}
