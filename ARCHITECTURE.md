# Architecture Overview

This document provides a detailed view of the Nexora system design and component interactions.

**For comprehensive requirements and technical specifications, see:**
- **Business Requirements:** [docs/BRD.md](./BRD.md)
- **Technical Requirements:** [docs/TRD.md](./TRD.md)

---

## System Flow

### 1. Search Request Pipeline

```
Client Request
    ↓
API Gateway (Rate Limit, Auth, TLS)
    ↓
Search API (.NET 10)
    ├─ Validate & Bind Parameters
    ├─ Query Processor
    │  ├─ Normalization (lowercase, trim)
    │  ├─ Spell Correction ("runng" → "running")
    │  └─ Synonym Expansion ("shoes" → ["shoes", "footwear", "trainers"])
    ├─ Cache Check (Valkey)
    │  ├─ HIT → Return Cached Response
    │  └─ MISS → Continue to Typesense
    ├─ Typesense Search
    │  ├─ Full-Text Query
    │  ├─ Hard Filters (stock_status, category)
    │  └─ Return Candidate Set
    ├─ Ranking Engine
    │  ├─ Compute FinalScore per Result
    │  ├─ Apply Business Rules (featured boosts, demotions)
    │  └─ Sort by Score
    ├─ Personalization Layer (Phase 2+)
    ├─ Pagination & Assembly
    ├─ Cache Write (Valkey, TTL 60s)
    └─ Response → Client
         ↓
    Analytics Event (Async)
         ↓
    RabbitMQ Event Bus
         ↓
    Event Aggregator / DWH
```

---

## 2. Data Synchronization

### Product Data Flow (MSSQL → Typesense)

```
MSSQL Server (Source of Truth)
├─ products table (catalog)
├─ stock table (inventory)
├─ pricing table (prices & discounts)
└─ attributes table (color, size, etc.)
    ↓ Change-Data-Capture (CDC)
    ├─ Enabled on products, stock, pricing
    ├─ Polled every 10 seconds
    └─ Captures INSERT/UPDATE/DELETE
    ↓
Index Sync Service (.NET 10 Worker)
├─ Reads CDC changes
├─ Batches into upsert requests
├─ Calls Typesense API
└─ Handles retry & dead-letter
    ↓
Typesense Cluster
├─ Upsert new/updated documents
├─ Apply schema transformations
└─ Update search index
```

**Field Mapping (MSSQL → Typesense):**

| MSSQL Column | Typesense Field | Transform |
|---|---|---|
| `ProductId` | `id` | UUID |
| `ProductName` | `title` | Indexed |
| `Description` | `description` | Indexed |
| `BrandId` / `BrandName` | `brand` | Facet |
| `CategoryId` / `CategoryName` | `category` | Facet, array |
| `Price` | `price` | Numeric, sortable |
| `Currency` | `currency` | String |
| `StockQuantity` | `stock_qty` | Numeric |
| `IsActive` | `is_active` | Boolean (filter) |
| `Rating` | `rating` | Numeric (0–5) |
| `ReviewCount` | `review_count` | Numeric |
| `IsFeatured` | `is_featured` | Boolean (filter) |
| `MerchantId` | `merchant_id` | String (filter) |
| `Color` (CSV) | `colors` | Array (split from CSV) |
| `Size` (CSV) | `sizes` | Array (split from CSV) |
| `CreatedAt` | `created_at` | Unix epoch int64 |
| `UpdatedAt` | `updated_at` | Unix epoch int64 |

---

## 3. Ranking Engine

The ranking formula combines relevance, behavioral signals, and business rules:

```
FinalScore = 
  0.40 × TextScore          (BM25 from Typesense)
+ 0.20 × CTR               (Click-through rate from analytics)
+ 0.15 × ConversionRate    (Purchase signals from events)
+ 0.10 × Availability      (Stock level: in_stock +0.1, low_stock +0.05, out_of_stock -0.5)
+ 0.10 × Rating            (Customer ratings: (rating / 5.0) × 0.10)
+ 0.05 × Personalization   (User affinity, Phase 2+)

Business Adjustments:
  × 1.5 if is_featured = true
  × 1.2 if is_new_product (created < 7 days ago)
  × 0.2 if out_of_stock = true
```

### Score Computation

1. **TextScore** — Retrieved from Typesense BM25 ranking (0–100 normalized)
2. **CTR & Conversion** — Looked up from PostgreSQL `ranking_signals` table (or Valkey cache)
3. **Availability** — Computed from stock_qty field in the result
4. **Rating** — Retrieved from product document in Typesense
5. **Personalization** — Injected from user profile service (Phase 2+)

---

## 4. Caching Strategy

### Valkey Cache Layers

| Cache Key | TTL | Content | Invalidation |
|---|---|---|---|
| `search::{query_hash}::{filters_hash}::{page}` | 60s | Full search response (paginated) | Query execution or manual |
| `suggest::{prefix}` | 30s | Top 8 typeahead suggestions | Query execution or manual |
| `ranking_signals::{product_id}` | 3600s (1h) | CTR, conversion rate, rating | Batch update (hourly) |
| `user_profile::{user_id}` | 1800s (30m) | User affinity signals, preferences | User action or manual invalidation |
| `rate_limit::{api_key}` | 60s | Request counter for sliding window | Automatic expiry |

**Cache Invalidation Triggers:**
- Search result cache cleared on product data updates (CDC event → publish to RabbitMQ → invalidate matching cache keys)
- Typeahead cache cleared on product catalog changes
- Manual override via Admin Dashboard

---

## 5. Security & Data Protection

### Authentication & Authorization

- **External APIs:** JWT tokens (OpenID Connect compatible)
- **Internal Services:** mTLS + service accounts
- **Admin APIs:** API keys + role-based access control (RBAC)

### Data Encryption

- **In Transit:** TLS 1.3 (all external + inter-service communication)
- **At Rest:** 
  - MSSQL Server: Transparent Data Encryption (TDE)
  - PostgreSQL: Encrypted volumes + column-level encryption for sensitive data
  - Valkey: Encryption at rest (via volume encryption)

### Secrets Management

- All connection strings, API keys, tokens stored in **AWS Secrets Manager**
- Rotated automatically (60-day cycle)
- Accessed via IAM roles (no hardcoded secrets)

---

## 6. Infrastructure Topology

```
AWS Account
├─ VPC (Private subnets for data services)
│  ├─ Amazon EKS Cluster
│  │  ├─ Search API pods (≥3 replicas)
│  │  ├─ Index Sync Worker pods (≥2 replicas)
│  │  ├─ Admin API pods (≥2 replicas)
│  │  └─ Typesense pods (≥3 nodes, anti-affinity)
│  ├─ RDS for PostgreSQL (Multi-AZ)
│  ├─ RDS for MSSQL Server (Multi-AZ)
│  ├─ ElastiCache for Valkey (Multi-AZ cluster mode)
│  └─ AmazonMQ for RabbitMQ (Multi-AZ)
├─ NAT Gateway (Outbound internet)
├─ CloudFront CDN (API edge caching)
├─ CloudWatch (Logs, metrics, alarms)
├─ Secrets Manager (Credentials & API keys)
└─ S3 (Backups, logs archival)
```

---

## 7. Observability

### Traces

- **Instrumentation:** OpenTelemetry SDK in .NET 10
- **Exporter:** OTLP → AWS CloudWatch
- **Traces Captured:**
  - Request entry → response exit
  - Typesense query latency
  - Cache hit/miss + lookup time
  - Database query time (PostgreSQL, MSSQL)
  - RabbitMQ publish latency

### Metrics

- **Prometheus Scrape Targets:** `/metrics` endpoint (Prometheus format)
- **Key Metrics:**
  - `search_query_latency_ms` (histogram: p50, p95, p99)
  - `cache_hit_ratio` (gauge, %)
  - `typesense_query_latency_ms`
  - `sync_lag_seconds` (Index Sync lag)
  - `errors_total` (counter, by error type)
  - `requests_per_second` (gauge)

### Dashboards (Grafana)

1. **Search Health:** Query latency, error rates, throughput
2. **Cache Performance:** Hit ratio, eviction rate, memory usage
3. **Index Sync:** Lag, error count, throughput
4. **Infrastructure:** Pod CPU/memory, network I/O, disk usage

---

## 8. Disaster Recovery & High Availability

| Component | RTO | RPO | Strategy |
|---|---|---|---|
| **Typesense Index** | 15 min | 5 min | Multi-replica cluster + S3 backup (hourly) |
| **PostgreSQL (metadata)** | 5 min | 1 min | RDS Multi-AZ + automated backups |
| **MSSQL Server (catalog)** | 5 min | 1 min | RDS Multi-AZ + automated backups |
| **Valkey Cache** | 30 sec | N/A (ephemeral) | Multi-AZ cluster mode; data loss acceptable |
| **RabbitMQ** | 10 min | 1 min | Multi-AZ broker; durable queues |

---

**For more details, refer to the full [TRD.md](./TRD.md) and [BRD.md](./BRD.md).**
