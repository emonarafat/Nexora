# Nexora Load Tests — Phase 1.7

This directory contains load and performance test scripts for the Nexora Search API.

## 🎯 Performance Targets

As defined in Phase 1.7 requirements:

| Metric | Target | Current |
|--------|--------|---------|
| **P95 Latency** | < 100ms | - |
| **P99 Latency** | < 200ms | - |
| **Error Rate** | < 0.1% | - |
| **Cache Hit Ratio** | ≥ 40% | - |
| **Zero Result Rate** | < 15% improvement | - |

## 📋 Prerequisites

### Install k6

**macOS:**
```bash
brew install k6
```

**Ubuntu/Debian:**
```bash
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6
```

**Windows:**
```powershell
choco install k6
```

**Docker:**
```bash
docker pull grafana/k6:latest
```

## 🚀 Running Load Tests

### Basic Test (Local Development)

```bash
# Start the Search API locally first
cd src/Nexora.SearchAPI
dotnet run

# In another terminal, run the load test
cd load-tests
k6 run search-load-test.js
```

### Test Against Specific Environment

```bash
# Development environment
BASE_URL=https://api-dev.nexora.com k6 run search-load-test.js

# Staging environment
BASE_URL=https://api-staging.nexora.com AUTH_TOKEN=your_token k6 run search-load-test.js

# Production (with authentication)
BASE_URL=https://api.nexora.com AUTH_TOKEN=your_prod_token k6 run search-load-test.js
```

### Generate HTML Report

```bash
k6 run --out json=results.json search-load-test.js
```

The test will automatically generate:
- `load-test-results.html` - Interactive HTML report
- `load-test-summary.json` - JSON summary for CI/CD

### Integration with Grafana

```bash
# Start local Grafana + InfluxDB
docker-compose -f ../infra/docker-compose.monitoring.yml up -d

# Run test with InfluxDB output
k6 run --out influxdb=http://localhost:8086/k6 search-load-test.js

# View results in Grafana at http://localhost:3000
```

## 📊 Test Scenarios

### Scenario 1: Ramp-Up (0 → 3000 req/s)

Tests scalability and identifies breaking points:

1. **0 → 500 req/s** (1 minute) - Warm-up phase
2. **500 → 1000 req/s** (2 minutes) - Baseline peak load
3. **1000 → 3000 req/s** (2 minutes) - 3× peak stress test

### Scenario 2: Sustained Load (2000 req/s for 10 minutes)

Tests stability at 2× peak load:

- **Duration:** 10 minutes
- **Rate:** 2000 req/s constant
- **Purpose:** Identify memory leaks, connection pool issues, cache stability

### Scenario 3: Concurrent Typeahead (500 req/s)

Tests suggest/typeahead endpoint performance:

- **Duration:** 16 minutes (runs concurrent with main scenarios)
- **Rate:** 500 req/s constant
- **Target:** < 50ms P95 latency

## 🧪 Test Data

The load test uses realistic query patterns:

- **High-frequency queries:** "running shoes", "laptop", "nike"
- **Category queries:** "leather sofas brown", "men's athletic shoes"
- **Typo queries:** "snekars", "laptp" (spell correction test)
- **SKU lookups:** "SKU-ABC123", "PROD-98765" (navigational intent)
- **Synonym queries:** "couch", "TV" (synonym expansion test)

## 📈 Analyzing Results

### Key Metrics to Monitor

1. **Latency Distribution:**
   - P50 (median)
   - P95 (95th percentile) - **Target: < 100ms**
   - P99 (99th percentile) - **Target: < 200ms**

2. **Error Rate:**
   - HTTP 4xx errors (client errors)
   - HTTP 5xx errors (server errors)
   - **Target: < 0.1%**

3. **Cache Efficiency:**
   - Cache hit ratio via `X-Cache` header
   - **Target: ≥ 40%**

4. **Query Quality:**
   - Zero result rate
   - **Target: < 15% of baseline**

### HTML Report

Open `load-test-results.html` in a browser to view:

- Request rate over time
- Latency trends
- Error breakdown
- Custom metric visualization

### JSON Summary

```bash
cat load-test-summary.json | jq '.metrics.http_req_duration.values'
```

## 🔧 Customizing Tests

### Adjust Virtual Users

Edit `search-load-test.js`:

```javascript
scenarios: {
  ramp_up: {
    preAllocatedVUs: 200,  // Increase for more concurrent users
    maxVUs: 1000,          // Maximum VUs
    // ...
  }
}
```

### Adjust Test Duration

```javascript
stages: [
  { duration: '5m', target: 1000 },  // Extend ramp-up to 5 minutes
  { duration: '20m', target: 2000 }, // Sustained load for 20 minutes
]
```

### Add Custom Queries

```javascript
const SEARCH_QUERIES = [
  'running shoes',
  'your custom query',
  // ...
];
```

## 🐛 Troubleshooting

### High Error Rate

```bash
# Check API logs
kubectl logs -f deployment/nexora-searchapi -n nexora

# Check resource limits
kubectl top pods -n nexora
```

### High Latency

1. **Cache warming:** Ensure cache is populated before load test
2. **Database connection pool:** Check PostgreSQL connection limits
3. **Typesense cluster:** Monitor Typesense query latency

### Connection Refused

```bash
# Verify API is running
curl http://localhost:5000/health

# Check firewall/network
telnet localhost 5000
```

## 📝 CI/CD Integration

Load tests run automatically in CI on main branch merges:

```yaml
# .github/workflows/load-test.yml
- name: Run Load Test
  run: |
    k6 run --quiet --no-color \
      --out json=results.json \
      load-tests/search-load-test.js

- name: Validate Thresholds
  run: |
    # CI fails if thresholds not met
    jq -e '.metrics.http_req_duration.values["p(95)"] < 100' results.json
```

## 🎓 Learning Resources

- [k6 Documentation](https://k6.io/docs/)
- [k6 Best Practices](https://k6.io/docs/testing-guides/test-types/)
- [Performance Testing Patterns](https://k6.io/docs/testing-guides/running-large-tests/)

## 📧 Support

For issues with load tests:

1. Check [load test logs](./logs/)
2. Review [performance baselines](../docs/performance-baseline.md)
3. Open issue: [GitHub Issues](https://github.com/emonarafat/Nexora/issues)
