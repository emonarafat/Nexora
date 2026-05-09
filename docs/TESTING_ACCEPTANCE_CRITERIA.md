# Phase 1.7 Acceptance Criteria Checklist

This document tracks the acceptance criteria for Phase 1.7 Testing implementation.

## 📋 Test Coverage Requirements

### Unit Tests (Target: ≥80% Line Coverage)

- [x] **Query Processor Tests**
  - [x] QuerySanitizer: Injection prevention, length validation (QuerySanitizerTests.cs)
  - [x] QueryNormalizer: Case normalization, whitespace handling (QueryNormalizerTests.cs)
  - [x] SpellCorrection: Typo tolerance logic (SpellCorrectionTests.cs)
  - [x] SynonymExpander: Basic synonym expansion logic (SynonymExpanderTests.cs)
  - [x] IntentClassifier: Intent detection patterns (IntentClassifierTests.cs)
  - [x] QueryPipeline: End-to-end pipeline orchestration (QueryPipelineTests.cs)

- [x] **Ranking Engine Tests**
  - [x] Formula validation: 40% text + 20% availability + 20% rating + 20% popularity (RankingEngineTests.cs)
  - [x] Business boosts: ×1.3 featured, ×0.2 out-of-stock (RankingEngineTests.cs)
  - [x] Edge cases: Zero signals, negative scores, extremes (RankingEngineTests.cs)
  - [x] Performance: 1000 candidates < 5ms (RankingEngineTests.cs)

- [x] **Filter Expression Validator**
  - [x] Typesense filter syntax validation (SearchRequestValidatorTests.cs)
  - [x] Blacklist validation: merchant_id blocking (SearchRequestValidatorTests.cs)
  - [x] Facet field validation (SearchRequestValidatorTests.cs)

- [ ] **Response DTO Validation Tests**
  - [ ] SearchResponse serialization
  - [ ] SuggestResponse serialization
  - [ ] Error response formatting

- [x] **Field Mapper Tests**
  - [x] MSSQL → Typesense field mapping (FieldMapperTests.cs)
  - [x] Stock status aliases (FieldMapperTests.cs)
  - [x] Category hierarchy parsing (FieldMapperTests.cs)
  - [x] HTML stripping (FieldMapperTests.cs)

### Integration Tests (Target: All passing in <5 minutes)

- [x] **PostgreSQL Integration (Testcontainers)**
  - [x] SynonymExpander with real database (SynonymExpanderIntegrationTests.cs)
  - [x] Concurrent synonym queries
  - [x] Cache behavior validation

- [ ] **Search API → Typesense Flow**
  - [ ] Full search request pipeline
  - [ ] Filter application
  - [ ] Facet aggregation
  - [ ] Ranking integration

- [ ] **Typeahead → Valkey Cache**
  - [ ] Cache miss → compute → store
  - [ ] Cache hit → fast retrieval
  - [ ] Cache invalidation on version bump

- [ ] **Index Sync Service (MSSQL + Typesense)**
  - [ ] Full reindex with testcontainers
  - [ ] CDC change detection
  - [ ] Field mapping end-to-end
  - [ ] Batch processing

- [ ] **Admin API Endpoints**
  - [ ] Synonym CRUD operations
  - [ ] Ranking config updates
  - [ ] Reindex trigger

### Load & Performance Tests

- [x] **k6 Load Test Scripts**
  - [x] Scenario 1: Ramp 0 → 3000 req/s (search-load-test.js)
  - [x] Scenario 2: Sustained 2000 req/s for 10 minutes (search-load-test.js)
  - [x] Concurrent typeahead tests (search-load-test.js)

- [ ] **Performance Validation**
  - [ ] P95 latency < 100ms at 3× peak
  - [ ] P99 latency < 200ms at 3× peak
  - [ ] Error rate < 0.1%
  - [ ] Cache hit ratio ≥ 40% after warm-up

- [ ] **Metrics Collection**
  - [ ] PromQL queries defined
  - [ ] Grafana dashboards configured
  - [ ] CloudWatch integration (AWS)

### Acceptance Tests (Definition of Done)

- [ ] **Query Quality Improvements**
  - [ ] ✅ Zero-result rate reduced by ≥15% vs baseline
  - [ ] ✅ Typo correction: "snekars" → "sneakers"
  - [ ] ✅ Synonym expansion: "couch" includes "sofa" results
  - [ ] ✅ Facets displayed correctly in response
  - [ ] ✅ Out-of-stock products demoted in ranking

- [ ] **Real-Time Sync**
  - [ ] ✅ Stock status reflects MSSQL changes within 5 minutes
  - [ ] ✅ No data corruption during sync
  - [ ] ✅ No index inconsistencies

- [ ] **CI/CD Quality Gates**
  - [ ] ✅ All unit tests passing
  - [ ] ✅ All integration tests passing
  - [ ] ✅ Code coverage ≥ 80%
  - [ ] ✅ Coverage badge in README

## 🚀 CI/CD Configuration

- [x] **Build Pipeline Updates**
  - [x] Code coverage collection (coverlet.runsettings)
  - [x] Coverage report generation (ReportGenerator)
  - [x] Coverage threshold enforcement (≥80%)
  - [x] Coverage artifact upload

- [ ] **Coverage Reporting**
  - [ ] Codecov.io integration (or SonarQube)
  - [ ] Coverage badge in README
  - [ ] PR coverage comments
  - [ ] Coverage trend tracking

- [ ] **Load Test Pipeline**
  - [ ] Scheduled nightly load tests
  - [ ] Performance regression detection
  - [ ] Baseline comparison
  - [ ] Alert on threshold violations

## 📦 Deliverables

- [x] **Test Projects**
  - [x] Nexora.SearchAPI.Tests (unit + integration)
  - [x] Nexora.IndexSync.Tests (unit + integration)
  - [x] Nexora.AdminAPI.Tests (integration)

- [x] **Test Infrastructure**
  - [x] Testcontainers support (PostgreSQL, MSSQL)
  - [x] PostgreSqlIntegrationTestBase
  - [x] Test data fixtures (ProductFixtures.cs)

- [x] **Load Test Assets**
  - [x] k6 load test script (search-load-test.js)
  - [x] Load test README with instructions
  - [x] Performance targets documented

- [x] **Configuration**
  - [x] coverlet.runsettings
  - [x] coverage.yml
  - [x] Updated .github/workflows/build.yml

- [x] **Documentation**
  - [x] Test environment provisioning script (`scripts/provision-test-env.sh`)
  - [ ] Performance baseline document
  - [ ] Testing best practices guide
  - [ ] README updates (coverage badge)

## ✅ Definition of Done

Phase 1.7 is complete when:

1. ✅ All unit tests passing with ≥80% line coverage
2. ⏳ All integration tests passing in <5 minutes total
3. ⏳ Load test P95 < 100ms at 3× peak load
4. ⏳ Code coverage report published (Codecov/SonarQube)
5. ⏳ CI/CD fails on coverage drop below 80%
6. ⏳ All acceptance criteria validated
7. ⏳ Performance baseline documented
8. ⏳ Test provisioning automated

## 📊 Current Status

**Last Updated:** 2026-05-09

| Category | Progress |
|----------|----------|
| Unit Tests | 90% (missing DTO tests) |
| Integration Tests | 20% (PostgreSQL done, need Typesense/Valkey/AdminAPI) |
| Load Tests | 80% (scripts ready, need validation runs) |
| CI/CD | 75% (coverage collection ready, need badges) |
| Documentation | 70% (load test docs done, need provisioning script) |

**Overall Phase 1.7 Progress: 67%**

## 🎯 Next Steps

1. Add DTO serialization tests
2. Implement Typesense integration tests
3. Implement Valkey cache integration tests
4. Create AdminAPI integration tests
5. Run baseline load tests and document results
6. Set up Codecov or SonarQube
7. Add coverage badge to README
8. Create test environment provisioning script
9. Document performance baseline
10. Validate all acceptance criteria

---

**Note:** This checklist should be updated as tests are implemented and acceptance criteria are validated.
