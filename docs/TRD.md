# Technical Requirements Document (TRD)

**Project Name:** Enterprise Search & Discovery System  
**Platform:** E-commerce Marketplace  
**Search Engine:** Typesense  
**API Stack:** .NET 10 Minimal API — Vertical Slice Architecture  
**Version:** 1.0  
**Date:** 2026-05-07  
**Status:** Draft for Engineering Review

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [System Flow](#2-system-flow)
3. [Technology Stack](#3-technology-stack)
4. [Index Design](#4-index-design)
5. [Product Data Ingestion](#5-product-data-ingestion)
6. [Ranking Strategy](#6-ranking-strategy)
7. [Query Processing Pipeline](#7-query-processing-pipeline)
8. [Filtering & Faceting](#8-filtering--faceting)
9. [Personalization Engine](#9-personalization-engine)
10. [Analytics Pipeline](#10-analytics-pipeline)
11. [API Contract](#11-api-contract)
12. [Performance Requirements](#12-performance-requirements)
13. [Optimization Loop](#13-optimization-loop)
14. [Security Requirements](#14-security-requirements)
15. [Infrastructure & Cost Optimization](#15-infrastructure--cost-optimization)
16. [Testing Strategy](#16-testing-strategy)
17. [Observability](#17-observability)

---

## 1. Architecture Overview

See [docs/ARCHITECTURE.md](../ARCHITECTURE.md) for detailed component interactions and data flows.

**High-level stack:** MSSQL Server → Index Sync Service → Typesense ← Search API ← Clients

**Technology:** .NET 10 Minimal API (Vertical Slice), Typesense, Valkey, RabbitMQ, PostgreSQL, AWS EKS

---

## 2. System Flow

### Search Request Pipeline

1. Client → API Layer (auth, validation)
2. Query Processor (normalize, spell check, synonyms, intent)
3. Cache check (Valkey)
4. Typesense retrieval
5. Ranking Engine (FinalScore computation)
6. Response assembly + cache write
7. Async analytics event publish

**Latency budget:** < 100ms P95

### Typeahead Flow

Debounced (300ms) prefix-based suggestions with < 50ms P95 latency.

---

## 3. Technology Stack

| Layer | Technology | Rationale |
|---|---|---|
| API Framework | .NET 10 Minimal API | Low overhead, native AOT compatible |
| Architecture Pattern | Vertical Slice Architecture | Feature isolation |
| Search Engine | Typesense | Sub-10ms queries, typo tolerance, low ops |
| Cache | Valkey 8 | Sub-millisecond reads, Redis-compatible |
| Message Bus | RabbitMQ | Durable analytics event delivery |
| Product Database | MSSQL Server | Primary product catalog — source of truth |
| Metadata Database | PostgreSQL | Synonyms, ranking config, A/B tests |
| Containerization | Docker + Amazon EKS | Horizontal scaling |
| CI/CD | GitHub Actions | Standard pipeline |
| Observability | OpenTelemetry + Grafana + Prometheus | Vendor-neutral tracing/metrics |
| APM | AWS CloudWatch | Latency percentiles, error tracking |

---

## 4. Index Design

### Collection: `products`

**Fields:** 24 total (text, numeric, array, bool)
- Searchable: `title` (weight 10), `brand` (5), `sku` (4), `description` (2), `category` (3)
- Facetable: `brand`, `category`, `category_path`, `color`, `size`, `rating`, `stock_status`, `merchant_id`
- Sortable: `price`, `rating`, `popularity_score`, `created_at`
- Numeric: `price`, `rating`, `stock_quantity`, `popularity_score`, `ctr_30d`, `conversion_rate_30d`

**Query weights:** Typesense BM25 with field-level tuning.

**Default sort:** `popularity_score` descending.

---

## 5. Product Data Ingestion

### Source of Truth

**MSSQL Server** is the single source of truth for all product data.

### Change-Data-Capture (Real-Time)

- CDC enabled on `products`, `stock`, `pricing` tables
- Polled every 10 seconds by Index Sync Service
- Batched upserts to Typesense via bulk import API
- Latency SLA: < 30 seconds for product create/update, < 5 minutes for stock/price changes

### Full Re-index (Weekly)

- Reads `vw_search_product_flat` (JOIN of all product tables)
- Pages of 1,000 rows
- Runs weekly off-peak (Sunday 02:00 UTC)
- Manual trigger available via Admin API
- Latency SLA: < 2 hours

### Field Mapping

20+ MSSQL columns mapped to Typesense fields with transforms (UTC epoch conversion, CSV splitting, boolean flags, etc.).

### Error Handling

- Retry with exponential back-off (max 5 attempts)
- Dead-letter table in MSSQL for poison records
- Prometheus gauge: `search_index_sync_lag_seconds`
- Alerting thresholds: > 5 min (stock), > 60 sec (product)

---

## 6. Ranking Strategy

### Base Relevance — BM25

Typesense computes BM25 text relevance score (0–100 normalized).

### Final Ranking Formula

$$\text{FinalScore} = 0.40 \times \text{TextScore} + 0.20 \times \text{CTR} + 0.15 \times \text{ConversionRate} + 0.10 \times \text{Availability} + 0.10 \times \text{Rating} + 0.05 \times \text{Personalization}$$

All signal values normalized to [0, 1].

### Boosts & Demotions

| Condition | Action |
|---|---|
| `is_featured = true` | × 1.3 |
| `stock_status = "low_stock"` | × 0.9 |
| `stock_status = "out_of_stock"` | × 0.0 or excluded |
| Rating < 2.5 (with >20 reviews) | × 0.7 |

### Weights are Runtime-Configurable

Stored in PostgreSQL, loaded at startup, refreshed every 60 seconds. Changeable via Admin API.

---

## 7. Query Processing Pipeline

**Stages:**
1. Sanitization (trim, strip control chars)
2. Normalization (lowercase, Unicode NFC)
3. Spell correction (Typesense typo tolerance)
4. Synonym expansion (PostgreSQL cache, 5-min TTL)
5. Intent classification (rule-based Phase 1, ML Phase 3)
6. Query structuring (Typesense params)

**Typo tolerance:** num_typos = 1 for ≤8-char queries, 2 for longer.

---

## 8. Filtering & Faceting

### Supported Filters

| Filter | Type | Example |
|---|---|---|
| `price` | float range | `price:[10..250]` |
| `brand` | string exact or multi | `brand:=Nike` |
| `category` | string exact | `category:=Footwear` |
| `color` / `size` | string[] contains | `color:=[Red,Blue]` |
| `rating` | float gte | `rating:>=4` |
| `stock_status` | string exact | `stock_status:=in_stock` |
| `merchant_id` | string (server-injected) | — |

**Facet counts:** Returned per response, hidden if count = 0.

**Pagination:** Default 20 results/page, max 100. Deep pagination (page > 50) rate-limited.

---

## 9. Personalization Engine

### Phase 2 — Rule-Based

Signals: category affinity (7-day), brand affinity (90-day), price range (24h). Blended weight: 0.05 in FinalScore.

Profiles cached in Valkey (TTL 10 min), < 5ms fetch latency.

### Phase 3 — ML-Based

Weekly batch retrain on 90-day event data. Two-Tower or LightGBM. Fallback to rule-based if inference fails.

### Anonymous Users

Personalization weight = 0; TextScore weight increases to 0.45.

---

## 10. Analytics Pipeline

### Events Schema

CloudEvents v1.0 format published to RabbitMQ message bus.

**Event types:**
- `search.query` — Search execution
- `search.click` — Product clicked in results
- `search.add_to_cart` — Product added to cart
- `search.purchase` — Purchase completed

### Pipeline

Events → RabbitMQ → Stream Processor (aggregation) → DWH (BigQuery/Synapse) + Data Lake (ADLS/S3) → Analytics Dashboard

### KPIs

- **CTR:** clicks / queries
- **Conversion:** purchases / queries
- **Zero-result rate:** queries with 0 results
- **Avg latency:** from search.query events

---

## 11. API Contract

### Search Endpoint

```
GET /api/v1/search?q=running+shoes&page=1&per_page=20&sort=relevance&filter_by=...
```

**Response:** 284 results object with facets, latency_ms, corrected_query.

### Typeahead Endpoint

```
GET /api/v1/suggest?q=run&limit=8
```

**Response:** Array of suggestions with category metadata.

### Analytics Event Endpoint (Internal)

```
POST /api/v1/events
```

Fire-and-forget event logging.

### Admin Endpoints (Internal, Authenticated)

- `GET /admin/v1/synonyms`, `POST`, `DELETE` — Synonym management
- `GET /admin/v1/ranking-config`, `PUT` — Ranking weight tuning
- `GET /admin/v1/zero-result-queries` — Zero-result query review
- `GET /admin/v1/ab-tests`, `POST` — A/B testing

---

## 12. Performance Requirements

| Requirement | Target | Measurement |
|---|---|---|
| Search P95 latency | < 100ms | APM percentile |
| Search P99 latency | < 200ms | APM percentile |
| Typeahead P95 latency | < 50ms | APM percentile |
| Cache hit ratio | ≥ 40% | Valkey stats |
| Typesense query time | < 20ms | Typesense metrics |
| Uptime SLA | 99.9% monthly | Uptime monitoring |
| Index freshness (stock) | ≤ 5 min lag | Monitoring alert |

### Caching Strategy

| Cache Key | TTL | Invalidation |
|---|---|---|
| Search result | 60s | Explicit on index update |
| Typeahead suggestion | 30s | Time-based |
| User profile | 600s | Explicit on profile update |
| Synonyms | 300s | Explicit on admin update |

---

## 13. Optimization Loop

### Weekly Cadence

1. **Zero-result query review** — Add synonyms, adjust mappings
2. **Low-CTR query review** — Adjust ranking, add boosts
3. **Ranking weight validation** — Compare A/B results, update weights
4. **Synonym maintenance** — Clean up stale rules
5. **KPI trend review** — Escalate if any metric degrades > 10% WoW

---

## 14. Security Requirements

### Authentication

- **Public API:** API key via `X-API-Key` header
- **Admin API:** JWT (1h expiry) via internal IdP
- **Analytics:** Service identity + mutual TLS
- **Typesense admin:** Scoped API keys (never exposed)

### Input Validation

- Query: max 200 chars, XSS/injection pattern rejection
- Filters: validated allow-list schema, `merchant_id` server-injected only
- Events: strict schema validation

### Rate Limiting

| Endpoint | Limit |
|---|---|
| Search | 60 req/min per API key |
| Suggest | 120 req/min per API key |
| Events | 1000 events/min per service |
| Admin | 30 req/min per user |

### Data Protection

- No PII in search index
- `user_id` in analytics: pseudonymous (hashed)
- TLS 1.2+ for all transit
- Encrypted at rest (cloud defaults)
- Secrets via AWS Secrets Manager

---

## 15. Infrastructure & Cost Optimization

### Typesense Deployment

- **Prod:** 3-node cluster (leader + 2 replicas), 4vCPU/8GB RAM per node
- **Staging:** Single node
- **Index size:** ~500MB for 1M products

### Cost Controls

- Valkey cache: ≥ 40% reduction in Typesense load
- Analytics: 90-day raw retention, indefinite aggregated
- K8s: HPA min=2, max=10; scale on CPU 70% + P95 latency
- All infrastructure: Terraform/CDK (no manual provisioning)

---

## 16. Testing Strategy

### Test Coverage

- **Unit tests:** ≥ 80% line coverage (Query processor, Ranking, Filter parser)
- **Integration tests:** Testcontainers (Typesense, PostgreSQL, Valkey)
- **Contract tests:** OpenAPI schema validation on every CI run
- **Load tests:** k6 ramp to 3× peak QPS, verify P95 < 100ms
- **A/B tests:** 95% confidence, 2-week min duration, 5k searches/variant

### Acceptance Criteria

- Unit coverage ≥ 80%
- Integration: all critical paths covered
- P95 latency (load test) < 100ms at 2× peak
- OWASP Top 10 scan clean
- Zero-result rate matches/improves baseline

---

## 17. Observability

### Metrics

- `search_requests_total` (counter)
- `search_latency_ms` (histogram)
- `search_cache_hits_total` (counter)
- `search_typesense_latency_ms` (histogram)
- `search_zero_results_total` (counter)
- `search_ranking_score_avg` (gauge)

### Distributed Tracing (OpenTelemetry)

End-to-end tracing: API → QueryProcessor → CacheCheck → Typesense → RankingEngine → Response. Trace IDs correlated with analytics events.

### Dashboards (Grafana)

1. **Search Health:** Latency percentiles, QPS, error rate, cache hit ratio
2. **Search Quality:** CTR, conversion, zero-result rate, top queries
3. **Infrastructure:** Typesense node health, Valkey memory, K8s pod count

### Alerts

| Alert | Condition | Severity |
|---|---|---|
| High latency | P95 > 150ms (5 min) | Warning |
| Very high latency | P95 > 300ms (2 min) | Critical |
| High error rate | 5xx > 1% (5 min) | Critical |
| Zero-result spike | Rate > 15% (10 min) | Warning |
| Cache miss spike | Hit ratio < 20% (15 min) | Warning |
| Index staleness | Stock sync lag > 10 min | Warning |

---

**For detailed architecture flows, data mappings, and deployment guide, see [docs/ARCHITECTURE.md](../ARCHITECTURE.md).**

*Document Owner: Engineering Lead*  
*Last Updated: 2026-05-07*  
*Next Review: End of Phase 1*
