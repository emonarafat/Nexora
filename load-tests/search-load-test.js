/**
 * Nexora Search API Load Test - Phase 1.7
 *
 * Scenario 1: Ramp search requests from 0 to 3000 req/s
 * Scenario 2: Sustained 2× peak for 10 minutes
 *
 * Performance Targets:
 * - P95 latency < 100ms
 * - P99 latency < 200ms
 * - Error rate < 0.1%
 * - Cache hit ratio ≥ 40% after warm-up
 *
 * Usage:
 *   k6 run --out json=results.json load-tests/search-load-test.js
 *   k6 run --out influxdb=http://localhost:8086/k6 load-tests/search-load-test.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { htmlReport } from 'https://raw.githubusercontent.com/benc-uk/k6-reporter/2.4.0/dist/bundle.js';

// ─── Custom Metrics ──────────────────────────────────────────────────────────

const errorRate = new Rate('error_rate');
const searchLatency = new Trend('search_latency', true);
const cacheHitRate = new Rate('cache_hit_rate');
const zeroResultRate = new Rate('zero_result_rate');
const searchCounter = new Counter('search_requests');

// ─── Configuration ───────────────────────────────────────────────────────────

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const AUTH_TOKEN = __ENV.AUTH_TOKEN || '';

// Test scenarios
export const options = {
  scenarios: {
    // Scenario 1: Ramp from 0 → 3000 req/s over 5 minutes
    ramp_up: {
      executor: 'ramping-arrival-rate',
      startRate: 0,
      timeUnit: '1s',
      preAllocatedVUs: 100,
      maxVUs: 500,
      stages: [
        { duration: '1m', target: 500 },   // 0 → 500 req/s
        { duration: '2m', target: 1000 },  // 500 → 1000 req/s (baseline peak)
        { duration: '2m', target: 3000 },  // 1000 → 3000 req/s (3× peak)
      ],
      gracefulStop: '30s',
      exec: 'searchScenario',
    },

    // Scenario 2: Sustained 2× peak (2000 req/s) for 10 minutes
    sustained_load: {
      executor: 'constant-arrival-rate',
      rate: 2000,
      timeUnit: '1s',
      duration: '10m',
      preAllocatedVUs: 200,
      maxVUs: 400,
      gracefulStop: '30s',
      startTime: '6m', // Start after ramp-up completes
      exec: 'searchScenario',
    },

    // Concurrent typeahead/suggest tests
    typeahead_load: {
      executor: 'constant-arrival-rate',
      rate: 500,
      timeUnit: '1s',
      duration: '16m',
      preAllocatedVUs: 50,
      maxVUs: 100,
      gracefulStop: '20s',
      exec: 'typeaheadScenario',
    },
  },

  thresholds: {
    // Phase 1 requirements
    'http_req_duration{name:search}': ['p(95)<100', 'p(99)<200'],
    'error_rate': ['rate<0.001'], // < 0.1%
    'http_req_failed': ['rate<0.001'],
    'zero_result_rate': ['rate<0.15'], // Target: reduce by ≥15% vs baseline
    // Note: cache_hit_rate is tracked as a metric but not enforced via threshold
    // because the API does not emit an X-Cache header; the metric is informational only.
  },
};

// ─── Test Data ───────────────────────────────────────────────────────────────

const SEARCH_QUERIES = [
  // High-frequency queries
  'running shoes', 'nike', 'laptop', 'iphone', 'coffee maker',
  'yoga mat', 'headphones', 'backpack', 'wireless mouse', 'sneakers',

  // Category queries
  'leather sofas brown', 'men\'s athletic shoes', 'women\'s dresses',
  'kitchen appliances', 'outdoor furniture',

  // Typos (spell correction test)
  'snekars', 'laptp', 'tabel', 'coffe', 'wireles',

  // SKU patterns (navigational intent)
  'SKU-ABC123', 'PROD-98765', 'ABC-12345',

  // Synonym expansion test
  'couch', 'sofa', 'settee', 'TV', 'television',

  // Long-tail queries
  'best running shoes for marathon training',
  'affordable bluetooth headphones with noise cancellation',
  'ergonomic office chair under $300',
];

const FILTERS = [
  '',
  'category:=Footwear',
  'brand:=[Nike,Adidas]',
  'price:[50..200]',
  'rating:>=4.0',
  'stock_status:=in_stock',
  'category:=Electronics && price:[100..500]',
];

const SORT_MODES = ['relevance', 'price_asc', 'price_desc', 'rating', 'newest'];

// ─── Helper Functions ────────────────────────────────────────────────────────

function randomChoice(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

function getHeaders() {
  const headers = {
    'Content-Type': 'application/json',
  };
  if (AUTH_TOKEN) {
    headers['Authorization'] = `Bearer ${AUTH_TOKEN}`;
  }
  return headers;
}

// ─── Test Scenarios ──────────────────────────────────────────────────────────

export function searchScenario() {
  const query = randomChoice(SEARCH_QUERIES);
  const filter = randomChoice(FILTERS);
  const sort = randomChoice(SORT_MODES);
  const page = Math.random() > 0.9 ? Math.floor(Math.random() * 3) + 2 : 1; // 90% page 1, 10% page 2-4

  let url = `${BASE_URL}/api/v1/search?q=${encodeURIComponent(query)}&page=${page}&per_page=24&sort=${sort}`;
  if (filter) {
    url += `&filter_by=${encodeURIComponent(filter)}`;
  }

  const params = {
    headers: getHeaders(),
    tags: { name: 'search' },
  };

  const response = http.get(url, params);

  // Record metrics
  searchCounter.add(1);
  searchLatency.add(response.timings.duration);

  // Check response
  const success = check(response, {
    'status is 200': (r) => r.status === 200,
    'response time < 100ms (p95 target)': (r) => r.timings.duration < 100,
    'response time < 200ms (p99 target)': (r) => r.timings.duration < 200,
    'has results field': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.results !== undefined;
      } catch {
        return false;
      }
    },
  });

  if (!success) {
    errorRate.add(1);
  } else {
    errorRate.add(0);
  }

  // Parse response body
  try {
    const body = JSON.parse(response.body);

    // Check for zero results (SearchResponse.TotalCount serialised as totalCount)
    if (body.totalCount === 0) {
      zeroResultRate.add(1);
    } else {
      zeroResultRate.add(0);
    }

    // Track cache hits using the cacheHit field in the response body
    if (body.cacheHit === true) {
      cacheHitRate.add(1);
    } else {
      cacheHitRate.add(0);
    }
  } catch (e) {
    console.error('Failed to parse response:', e.message);
  }

  // Random think time (0-500ms)
  sleep(Math.random() * 0.5);
}

export function typeaheadScenario() {
  const prefixes = ['run', 'shoe', 'lap', 'cof', 'nik', 'adi', 'san', 'hea', 'bac', 'wir'];
  const prefix = randomChoice(prefixes);

  const url = `${BASE_URL}/api/v1/suggest?q=${encodeURIComponent(prefix)}&limit=8`;
  const params = {
    headers: getHeaders(),
    tags: { name: 'typeahead' },
  };

  const response = http.get(url, params);

  const success = check(response, {
    'status is 200': (r) => r.status === 200,
    'response time < 50ms (typeahead target)': (r) => r.timings.duration < 50,
    'has suggestions': (r) => {
      try {
        const body = JSON.parse(r.body);
        // /api/v1/suggest returns a JSON array of SuggestionItem objects directly
        return Array.isArray(body) && body.length > 0;
      } catch {
        return false;
      }
    },
  });

  if (!success) {
    errorRate.add(1);
  } else {
    errorRate.add(0);
  }

  // Typeahead requests are rapid-fire
  sleep(0.1);
}

// ─── Test Lifecycle ──────────────────────────────────────────────────────────

export function setup() {
  console.log('=== Load Test Starting ===');
  console.log(`Target: ${BASE_URL}`);
  console.log('Scenarios:');
  console.log('  1. Ramp 0 → 3000 req/s over 5 minutes');
  console.log('  2. Sustained 2000 req/s for 10 minutes');
  console.log('  3. Typeahead 500 req/s concurrent');
  console.log('Performance Targets:');
  console.log('  - P95 latency < 100ms');
  console.log('  - P99 latency < 200ms');
  console.log('  - Error rate < 0.1%');
  console.log('  - Cache hit ratio ≥ 40%');
  console.log('==========================\n');
}

export function teardown(data) {
  console.log('\n=== Load Test Complete ===');
}

export function handleSummary(data) {
  return {
    'load-test-results.html': htmlReport(data),
    'load-test-summary.json': JSON.stringify(data, null, 2),
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
  };
}

function textSummary(data, options = {}) {
  const indent = options.indent || '';
  const enableColors = options.enableColors || false;

  let summary = '\n';
  summary += indent + '═══════════════════════════════════════════════════\n';
  summary += indent + '  Nexora Phase 1.7 Load Test Results\n';
  summary += indent + '═══════════════════════════════════════════════════\n\n';

  // Metrics summary
  const metrics = data.metrics;

  summary += indent + '📊 Performance Metrics:\n';
  summary += indent + '─────────────────────────────────────────────────\n';

  if (metrics.http_req_duration) {
    const latency = metrics.http_req_duration.values;
    summary += indent + `  Search P50: ${latency['p(50)'].toFixed(2)}ms\n`;
    summary += indent + `  Search P95: ${latency['p(95)'].toFixed(2)}ms ${latency['p(95)'] < 100 ? '✅' : '❌'}\n`;
    summary += indent + `  Search P99: ${latency['p(99)'].toFixed(2)}ms ${latency['p(99)'] < 200 ? '✅' : '❌'}\n`;
  }

  if (metrics.error_rate) {
    const errRate = metrics.error_rate.values.rate * 100;
    summary += indent + `  Error Rate: ${errRate.toFixed(3)}% ${errRate < 0.1 ? '✅' : '❌'}\n`;
  }

  if (metrics.cache_hit_rate) {
    const cacheRate = metrics.cache_hit_rate.values.rate * 100;
    summary += indent + `  Cache Hit Rate: ${cacheRate.toFixed(1)}% ${cacheRate >= 40 ? '✅' : '❌'}\n`;
  }

  if (metrics.zero_result_rate) {
    const zeroRate = metrics.zero_result_rate.values.rate * 100;
    summary += indent + `  Zero Result Rate: ${zeroRate.toFixed(1)}%\n`;
  }

  if (metrics.search_requests) {
    summary += indent + `  Total Requests: ${metrics.search_requests.values.count}\n`;
  }

  summary += indent + '\n';
  summary += indent + '═══════════════════════════════════════════════════\n';

  return summary;
}
