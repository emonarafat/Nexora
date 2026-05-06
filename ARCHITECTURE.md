# Nexora Search Platform — Architecture

> **Version:** 1.2 · **Last Updated:** 2026-05-07 · Aligned with [BRD v1.2](docs/BRD.md) and [TRD v1.2](docs/TRD.md)

---

## 1. High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                            CLIENT LAYER                                         │
│   Browser / Mobile App / Third-party Storefront                                 │
└────────────────────────────────┬────────────────────────────────────────────────┘
                                 │ HTTPS / REST
                                 ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                            API GATEWAY / CDN                                    │
│   AWS CloudFront + WAF + Rate Limiting                                          │
└────────────────────────────────┬────────────────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                          SEARCH API SERVICE                                     │
│   .NET 10 Minimal API · Vertical Slice Architecture · Native AOT                │
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────┐   │
│  │                      QUERY PROCESSING PIPELINE                          │   │
│  │  Sanitize → Normalize → Spell-correct → Synonym-expand → Classify →     │   │
│  │  Structure → [LLM Enrich] → [Safety Guard] → Retrieval Request          │   │
│  └─────────────────────────────────────────────────────────────────────────┘   │
└───────┬──────────────────────────────────────────────────────────┬─────────────┘
        │                                                          │ analytics events
        ▼                                                          ▼
┌───────────────────┐      ┌─────────────────────────────────────────────────┐
│   VALKEY 8        │      │           RABBITMQ (AmazonMQ)                   │
│  (ElastiCache)    │      │   Exchange: search-events                       │
│                   │      │   Durable queues, topic exchanges               │
│  Search cache     │      └────────────────────┬────────────────────────────┘
│  Typeahead cache  │                           │
│  User profiles    │                           ▼
│  Rate limiting    │              ┌────────────────────────┐
│  Synonyms cache   │              │   STREAM PROCESSOR     │
│  Ranking config   │              │   (Consumer Service)   │
└───────────────────┘              └────────┬───────────────┘
                                            │
        │                                   ▼
        ▼                    ┌──────────────────────────────────────────────┐
┌───────────────────┐        │   DATA WAREHOUSE / DATA LAKE                 │
│   TYPESENSE       │        │   BigQuery / Synapse + ADLS Gen2 / S3        │
│  (Self-hosted,    │        │   Parquet/Gzip · Athena ad-hoc queries       │
│   3-node cluster) │        └──────────────────────────────────────────────┘
│                   │
│  products (index) │
│  knowledge_chunks │
│  (Phase 4.5)      │
└───────────────────┘

┌───────────────────────────────────────────────────────────────────────────────┐
│                        DATA TIER                                               │
│                                                                                │
│  ┌─────────────────────┐   ┌──────────────────────┐   ┌──────────────────┐  │
│  │   MSSQL Server      │   │   PostgreSQL (RDS     │   │   VECTOR DB      │  │
│  │   (Primary Catalog) │   │    Multi-AZ)          │   │   (Phase 4)      │  │
│  │                     │   │                       │   │   Qdrant /       │  │
│  │  products, stock,   │   │  Synonyms, ranking    │   │   Weaviate /     │  │
│  │  pricing, attrs,    │   │  overrides, A/B test  │   │   Milvus HNSW    │  │
│  │  categories         │   │  config, knowledge    │   │                  │  │
│  │  CDC enabled        │   │  chunk metadata,      │   │  Product embed-  │  │
│  │                     │   │  conversations        │   │  dings 384-dim   │  │
│  └──────────┬──────────┘   └──────────────────────┘   └──────────────────┘  │
└─────────────┼──────────────────────────────────────────────────────────────────┘
              │ CDC polling (10s)
              ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                        BACKGROUND SERVICES                                      │
│                                                                                 │
│  Index Sync Service     — CDC → Typesense upserts (10s interval)                │
│  Batch Re-indexer       — Full re-index via vw_search_product_flat (Sun 02:00)  │
│  Knowledge Ingestion    — Doc chunk → embed → upsert knowledge_chunks (Ph 4.5) │
│  Embedding Service      — BAAI/bge-small-en-v1.5, 384-dim (Phase 4)            │
│  LLM Understanding Svc  — vLLM self-hosted + API fallback (Phase 4)            │
│  Recommendation Svc     — Precomputed candidates + online reranking (Ph 4.5)   │
│  Affinity Refresher     — Recomputes Valkey profiles nightly (03:00 UTC)        │
│  CTR Signal Aggregator  — S3/RabbitMQ → search_ctr_signals (02:00 UTC)         │
│  Conversion Aggregator  — purchase events → search_conversion_signals (02:30)  │
│  Partition Pruner       — Drop PostgreSQL partitions > 90 days (04:00 UTC)     │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Technology Stack

| Component | Technology | Notes |
|---|---|---|
| API Framework | .NET 10 Minimal API | Vertical Slice Architecture; native AOT compatible |
| Search Engine | Typesense (self-hosted) | Sub-10ms queries; BM25 + typo tolerance built-in |
| Cache / Session | Valkey 8 (ElastiCache) | Redis-compatible OSS fork; allkeys-lru eviction |
| Message Bus | RabbitMQ (AmazonMQ) | Durable queues; topic exchanges; CloudEvents v1.0 |
| Primary DB | MSSQL Server | Product catalog source of truth; CDC enabled |
| Metadata DB | PostgreSQL (RDS Multi-AZ) | Synonyms, ranking config, A/B tests, signal tables |
| Vector DB | Qdrant / Weaviate / Milvus | HNSW index; 384-dim embeddings (Phase 4) |
| Embedding Model | BAAI/bge-small-en-v1.5 | 384-dim; self-hosted (Phase 4) |
| LLM Runtime | vLLM (self-hosted) + API fallback | Query understanding, summaries, RAG (Phase 4) |
| Container Orchestration | Amazon EKS | HPA; zero-downtime deploys |
| CDN / Edge | AWS CloudFront + WAF | TLS termination; rate limiting |
| Observability | OpenTelemetry → CloudWatch | Prometheus + Grafana dashboards |
| IaC | Terraform / AWS CDK | No manual provisioning in production |
| Secrets | AWS Secrets Manager | Never in code or environment variables |
| CI/CD | GitHub Actions | Trivy/Snyk container scanning on every build |

---

## 3. System Flows

### 3.1 Standard Search Request (Phases 1–3)

```
Client
  │  GET /api/v1/search?q=...
  ▼
Search API
  │  1. Validate & sanitize input
  │  2. Check Valkey search cache
  │     └── Hit → return cached response
  │  3. Normalize, spell-correct, synonym-expand
  │  4. Classify intent (rule-based Ph1 / ML Ph3)
  │  5. Inject merchant_id filter (multi-tenancy)
  │  6. Fetch personalization profile from Valkey (if authenticated)
  │  7. Execute Typesense query
  │  8. Compute FinalScore (BM25 + behavioral signals + personalization)
  │  9. Write result to Valkey cache (60s TTL)
  │ 10. Publish search_query event → RabbitMQ (fire-and-forget)
  ▼
Client ← JSON response (results + facets + corrected_query + latency_ms)
```

### 3.2 Typeahead / Suggest

```
Client
  │  GET /api/v1/suggest?q=prefix
  ▼
Search API
  │  1. Sanitize, normalize prefix
  │  2. Check Valkey suggest cache (30s TTL, keyed on prefix_hash)
  │     └── Hit → return cached suggestions
  │  3. Typesense prefix search (title, brand, category)
  │  4. Write to Valkey suggest cache
  ▼
Client ← [{text, category}] suggestions
```

### 3.3 Premium Hybrid Search Flow (Phase 4)

```
Client (premium API key)
  │  GET /api/v1/search?q=...&search_mode=hybrid
  ▼
Search API
  │  1–4. Standard sanitize / normalize / synonyms / intent
  │  5. LLM Understanding Service
  │       ├── Intent refinement (navigational/transactional/informational/comparative)
  │       ├── Implicit filter extraction → structured JSON
  │       └── Expansion terms injection
  │       Cache key: llm_understanding::{query_hash} (TTL: 30 days)
  │  6. Safety guardrails (prompt-injection check, schema validation, tenant scope)
  │  7. Parallel retrieval:
  │       ├── Typesense BM25 keyword search → NormalizedKeywordScore
  │       └── Vector DB ANN search (HNSW) → NormalizedSemanticScore
  │  8. HybridScore fusion (0.40 x keyword + 0.30 x semantic + 0.30 x ML)
  │  9. Apply boosts/demotions → final ranked list
  │ 10. Optional: AI summary generation (TTL: 7 days per product+context)
  │ 11. Publish search_query + search_ai_understanding events → RabbitMQ
  ▼
Client ← ranked results + AI summary/compare fields
```

### 3.4 RAG Product Knowledge Flow (Phase 4.5)

```
Client  POST /api/v1/search/knowledge/ask
  ▼
Search API
  │  1. Validate tenant authorization for knowledge source
  │  2. Embed query (BAAI/bge-small-en-v1.5)
  │  3. ANN search over knowledge_chunks (Vector DB)
  │  4. Retrieve top-K knowledge chunks from Typesense / PostgreSQL
  │  5. LLM generates grounded answer with citation references
  │  6. Validate JSON schema + hallucination guard
  │  7. Cache: rag::{merchant_id}::{query_hash} (TTL: 24h)
  │  8. Publish search_rag_answer event → RabbitMQ
  ▼
Client ← {answer, citations[], confidence}
```

### 3.5 AI Recommendation Flow (Phase 4.5)

```
Client  GET /api/v1/recommendations?product_id=...
  ▼
Recommendation Service
  │  1. Check cache: reco::{context_hash}::{user_or_session} (TTL: 15m)
  │  2. Retrieve similar-product embeddings from Vector DB
  │  3. Score candidates: RecommendationScore formula
  │  4. Apply business constraints (exclude OOS, cap brand repetition,
  │     apply merchant blocklist/pinlist)
  │  5. Publish search_recommendation_impression event → RabbitMQ
  ▼
Client ← [{product_id, reason_code, score}]
```

---

## 4. Vertical Slice Architecture

```
src/
├── Search.Api/
│   ├── Program.cs                     # Minimal API bootstrapping
│   ├── Features/
│   │   ├── Search/                    # SearchEndpoint, Request, Response, Handler, Validator
│   │   ├── Suggest/                   # SuggestEndpoint, Request, Response, Handler
│   │   ├── Events/                    # TrackEventEndpoint, Handler
│   │   └── Admin/
│   │       ├── Synonyms/
│   │       ├── RankingConfig/
│   │       └── ZeroResultQueries/
│   ├── Infrastructure/
│   │   ├── Typesense/                 # TypesenseSearchClient, TypesenseIndexManager
│   │   ├── Cache/                     # ValkeyCache
│   │   ├── Analytics/                 # EventPublisher
│   │   ├── Personalization/           # PersonalizationService, UserProfileClient
│   │   └── Gdpr/                      # DataErasureService
│   └── Pipeline/
│       ├── QueryNormalizer.cs
│       ├── SpellCorrector.cs
│       ├── SynonymExpander.cs
│       ├── IntentClassifier.cs
│       └── RankingEngine.cs
├── Search.Tests/
│   ├── Unit/
│   ├── Integration/                   # Testcontainers: Postgres, RabbitMQ, Typesense, Valkey, LocalStack
│   └── Performance/                   # k6 load tests
└── Search.Admin/                      # Admin dashboard (separate service)
```

---

## 5. Index Design

### 5.1 `products` Collection (Typesense)

| Field | Type | Indexed | Purpose |
|---|---|---|---|
| `id` | string | Yes | Primary key (product_id as string) |
| `title` | string | Yes (weight 10) | Product name |
| `brand` | string | Yes (weight 5) | Brand name |
| `sku` | string | Yes (weight 4) | SKU code |
| `description` | string | Yes (weight 2) | Stripped HTML |
| `category` | string | Yes (weight 3) | Category name |
| `category_path` | string[] | Yes | Hierarchical path segments |
| `price` | float | Sortable/Filterable | Unit price |
| `currency` | string | — | Currency code |
| `color` | string[] | Facetable | Color variants |
| `size` | string[] | Facetable | Size variants |
| `rating` | float | Sortable/Filterable | Avg rating (0–5) |
| `rating_count` | int32 | Filterable | Number of ratings |
| `stock_status` | string | Filterable | `in_stock` / `out_of_stock` / `low_stock` |
| `stock_quantity` | int32 | Filterable | Quantity on hand |
| `is_featured` | bool | Filterable | Featured flag |
| `is_active` | bool | Filterable | Active flag |
| `merchant_id` | string | Filterable | Tenant scoping (server-injected) |
| `created_at` | int64 | Sortable | Unix epoch |
| `updated_at` | int64 | — | Unix epoch |
| `ctr_30d` | float | — | 30-day click-through rate |
| `conversion_rate_30d` | float | — | 30-day conversion rate |
| `popularity_score` | float | Sortable | Composite popularity |

### 5.2 `knowledge_chunks` Collection (Phase 4.5)

| Field | Type | Purpose |
|---|---|---|
| `id` | string | Chunk identifier |
| `merchant_id` | string | Tenant scoping |
| `source_type` | string | `manual` / `policy` / `faq` / `guide` |
| `source_id` | string | Source document identifier |
| `chunk_text` | string | 300–500 token content chunk |
| `embedding` | float[] (384) | BAAI/bge-small-en-v1.5 vector |
| `chunk_index` | int32 | Position within source document |
| `created_at` | int64 | Unix epoch |
| `updated_at` | int64 | Unix epoch |

### 5.3 Index Update Strategy

| Update Type | Mechanism | Frequency | SLA |
|---|---|---|---|
| Stock / Pricing | CDC polling | Every 10 seconds | ≤ 5 min lag |
| Product create / update | CDC polling | Every 10 seconds | ≤ 60 sec lag |
| Full re-index | Batch via `vw_search_product_flat` | Sunday 02:00 UTC | < 2 hour window |
| Knowledge chunks | Incremental ingestion | On source change | ≤ 30 min freshness |

---

## 6. Data Synchronization

### Field Mapping (MSSQL → Typesense)

| MSSQL Column | Typesense Field | Transform |
|---|---|---|
| `product_id` (int) | `id` (string) | `.ToString()` |
| `product_name` | `title` | None |
| `brand_name` | `brand` | None |
| `product_sku` | `sku` | None |
| `product_description` | `description` | Strip HTML tags |
| `category_name` | `category` | None |
| `category_hierarchy` | `category_path` | Split by `>` |
| `unit_price` | `price` | None |
| `currency_code` | `currency` | None |
| `color_variants` | `color` | Split CSV |
| `size_variants` | `size` | Split CSV |
| `avg_rating` | `rating` | None |
| `rating_count` | `rating_count` | None |
| `stock_status_code` | `stock_status` | `IN_STOCK` / `OUT_OF_STOCK` |
| `qty_on_hand` | `stock_quantity` | None |
| `is_featured_flag` | `is_featured` | Bit → bool |
| `is_active_flag` | `is_active` | Bit → bool |
| `merchant_id` | `merchant_id` | None |
| `created_date` | `created_at` | `DateTimeOffset` → Unix epoch (int64) |
| `modified_date` | `updated_at` | `DateTimeOffset` → Unix epoch (int64) |

### CDC Tables Tracked

| MSSQL Table | CDC Columns | Typesense Action |
|---|---|---|
| `products` | All columns | Full document upsert |
| `stock` | `stock_status`, `stock_quantity` | Partial field update |
| `pricing` | `price`, `currency` | Partial field update |

### Error Handling

- Failed upserts retried with exponential back-off (max 5 attempts)
- Poison records written to `sync_dead_letter` table with error details
- Sync lag exposed as Prometheus gauge: `search_index_sync_lag_seconds`
- Alert fires if lag exceeds **5 minutes** (stock) or **60 seconds** (product create/update)

---

## 7. Query Processing Pipeline

```
Input query string
       │
       ▼
1. Sanitization       — Trim whitespace; strip control chars and HTML; truncate to 200 chars
       │
       ▼
2. Normalization      — Lowercase; Unicode NFC; remove excessive punctuation
       │
       ▼
3. Spell Correction   — Typesense built-in typo tolerance
                        (num_typos=1 for <=8 chars, 2 for longer)
       │
       ▼
4. Synonym Expansion  — PostgreSQL synonym map; in-memory cache (5-min TTL)
                        Supports one-way and two-way synonyms
       │
       ▼
5. Intent Classification
   Phase 1 (rule-based):  SKU pattern → navigational | brand → brand-boosted
                           category term → category-filtered | default → transactional
   Phase 3 (ML-based):    replaces rule-based classifier
       │
       ▼
6. Query Structuring  — Final Typesense parameters; inject synonyms, filters, sort, pagination
       │
       ▼
7. LLM Enrichment     [Premium Mode — Phase 4]
                        Intent refinement; implicit filter extraction → structured JSON;
                        expansion terms injection
       │
       ▼
8. Safety Guardrails  [Premium Mode — Phase 4]
                        Prompt-injection and jailbreak checks; output JSON schema validation;
                        tenant-scope validation before retrieval/generation
       │
       ▼
Output: Retrieval request object(s) for keyword-only or hybrid search
```

---

## 8. Ranking Strategy

### 8.1 Base Relevance — BM25

Typesense BM25 with parameters k1=1.2, b=0.75.

**Field Weights:**
```
query_by: "title,brand,sku,description,category"
query_by_weights: "10,5,4,2,3"
```

### 8.2 Final Ranking Formula (Phases 1–3)

All signal values normalized to [0, 1] before blending.

```
FinalScore =
  (0.40 × TextScore)        // BM25 normalized relevance
+ (0.20 × CTR_30d)          // Click-through rate, 30-day rolling
+ (0.15 × ConversionRate)   // Purchase conversion rate
+ (0.10 × Availability)     // 1.0 = in_stock, 0.0 = out_of_stock
+ (0.10 × Rating)           // Normalized avg rating (0–5 → 0–1)
+ (0.05 × Personalization)  // Phase 2: user affinity signal
```

Weights are runtime-configurable via the Admin API. Defaults stored in PostgreSQL; refreshed every 60 seconds.

Anonymous users: Personalization weight (0.05) redistributed to TextScore (→ 0.45).

### 8.3 Boosts & Demotions

| Condition | Action | Magnitude |
|---|---|---|
| `is_featured = true` | Multiply FinalScore | × 1.3 |
| `stock_status = "low_stock"` | Demote | × 0.9 |
| `stock_status = "out_of_stock"` | Demote or exclude | × 0.0 (or filtered) |
| `rating < 2.5 AND rating_count > 20` | Demote | × 0.7 |
| Promotional campaign active | Admin-injected boost | × 1.1 – 2.0 (configurable) |

### 8.4 Phase 4 Hybrid Formula

For `search_mode=hybrid` (premium):

```
HybridScore =
  (0.40 × NormalizedKeywordScore)
+ (0.30 × NormalizedSemanticScore)
+ (0.30 × NormalizedPhase3MlScore)
```

### 8.5 Recommendation Ranking Formula (Phase 4.5)

```
RecommendationScore =
  (0.35 × SimilarityScore)
+ (0.30 × CoPurchaseScore)
+ (0.20 × SessionAffinityScore)
+ (0.10 × MarginScore)
+ (0.05 × AvailabilityScore)
```

### 8.6 Sort Modes

| Sort Mode | Typesense Parameter |
|---|---|
| `relevance` (default) | `_text_match:desc,popularity_score:desc` |
| `price_asc` | `price:asc` |
| `price_desc` | `price:desc` |
| `rating` | `rating:desc,rating_count:desc` |
| `newest` | `created_at:desc` |

---

## 9. Caching Strategy (Valkey)

| Cache Key Pattern | TTL | Invalidation Trigger |
|---|---|---|
| `search::{query_hash}::{filters_hash}::{page}` | 60s | Explicit purge on index update |
| `suggest::{prefix_hash}` | 30s | Time-based only |
| `user_profile::{user_id}` | 600s | Explicit purge on profile update |
| `synonyms` | 300s | Explicit purge on admin update |
| `ranking_config` | 60s | Explicit purge on admin update |
| `llm_understanding::{query_hash}` | 30 days | Explicit purge on prompt/model change |
| `summary::{product_id}::{context_hash}` | 7 days | Explicit purge on product detail update |
| `rag::{merchant_id}::{query_hash}` | 24h | Purge on knowledge source update |
| `reco::{context_hash}::{user_or_session}` | 15m | Purge on stock or merchandising update |
| `user:affinity:{user_id_hash}` | 90 days | Explicit purge on opt-out; refreshed nightly |

Eviction policy: `allkeys-lru`. Target cache hit ratio >= 40% for popular queries.

---

## 10. Personalization Engine

### Phase 2 — Rule-Based (Valkey Affinity Profiles)

| Signal | Weight | Decay |
|---|---|---|
| Recent category views (7-day) | 0.4 | Linear decay over 7 days |
| Purchased brands (90-day) | 0.35 | None |
| Price range of recent sessions | 0.25 | 24-hour window |

```
PersonalizationScore =
  (0.40 × category_affinity_score)
+ (0.35 × brand_affinity_score)
+ (0.25 × price_affinity_score)
```

- Cache key: `user:affinity:{SHA-256(user_id)}` · TTL: 90 days
- Max boost cap: 0.20 (prevents over-personalization)
- Affinity refresh CronJob: daily at 03:00 UTC
- GDPR opt-out: `DELETE /api/users/me/affinity` → clears Valkey profile + writes erasure audit record

### Phase 3 — ML-Based

- Model: Gradient Boosted Trees (LightGBM) or Two-Tower Neural Net
- Training cadence: weekly batch retrain on 90-day click/purchase data
- Fallback: rule-based scoring if ML inference fails

### Phase 4 — Semantic + Conversational

- Session/conversation context injected into personalization features
- Conversation state persisted in PostgreSQL (`search_conversations`, `conversation_turns`)
- Automatic degradation to Phase 3 ML if LLM or vector retrieval unavailable

---

## 11. Analytics Pipeline

### Event Bus Architecture

```
Search API (fire-and-forget)
       │
       ▼
RabbitMQ exchange: search-events
       │
       ▼
Stream Processor (consumer service)
       │
  ┌────┴────────────────────────────┐
  ▼                                 ▼
Aggregated KPIs                 Raw event store
(DWH: BigQuery / Synapse)       (Data Lake: ADLS Gen2 / S3 · Parquet/Gzip)
```

### CloudEvents v1.0 Event Types

| Event Type | Trigger |
|---|---|
| `search.query` | Every search request |
| `search.click` | Result click |
| `search.add_to_cart` | Add to cart from search |
| `search.purchase` | Purchase attributed to search |
| `search.ai_understanding` | LLM query understanding (Phase 4) |
| `search.ai_summary` | AI summary served (Phase 4) |
| `search.rag_answer` | RAG answer generated (Phase 4.5) |
| `search.recommendation_impression` | Recommendations rendered (Phase 4.5) |

### KPI Computations

| KPI | Formula |
|---|---|
| CTR | `COUNT(search_click) / COUNT(search_query)` per query |
| Conversion Rate | `COUNT(search_purchase) / COUNT(search_query)` per query |
| Zero-result rate | `COUNT(queries WHERE result_count = 0) / COUNT(search_query)` |
| Avg latency | `AVG(latency_ms)` from `search_query` events |

### Data Retention

| Data | Retention | Storage |
|---|---|---|
| Raw events (S3) | 365 days (Standard → Intelligent-Tiering Day 30 → Glacier Day 90 → Expire Day 365) | S3 Parquet/Gzip |
| PostgreSQL signal tables | 90 days (monthly partitions; pruned nightly at 04:00 UTC) | RDS PostgreSQL |
| Valkey affinity profiles | 90 days TTL (renewed on update) | ElastiCache Valkey |

---

## 12. API Contract (Summary)

### Public Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/v1/search` | Product search with facets, filters, pagination |
| `GET` | `/api/v1/suggest` | Typeahead prefix suggestions |
| `POST` | `/api/v1/events` | Analytics event ingestion (internal service) |

### Phase 4 Premium Extensions

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/v1/search/summary` | AI summary for a product in current search context |
| `POST` | `/api/v1/search/compare` | AI comparison insights for selected products |
| `POST` | `/api/v1/search/chat` | Conversational search turn |

### Phase 4.5 Premium Expansion

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/v1/search/knowledge/ask` | RAG answer with citations |
| `GET` | `/api/v1/recommendations` | Similar / cross-sell / bundle recommendations |

### Admin Endpoints (Internal, JWT-authenticated)

`/admin/v1/synonyms`, `/admin/v1/ranking-config`, `/admin/v1/zero-result-queries`, `/admin/v1/ab-tests`

### Authentication

| Surface | Method |
|---|---|
| Public Search API | `X-API-Key` header (per-client) |
| Admin API | JWT (1h expiry) via internal IdP |
| Analytics Event API | Internal service identity + mutual TLS |
| Typesense admin | Scoped API keys (never exposed outside cluster) |

---

## 13. Security & Data Protection

### Input Validation

- All query parameters validated against strict allow-list schemas (no passthrough to Typesense)
- `filter_by` parameter parsed and validated; `merchant_id` filter rejected from client input (server-injected only)
- Max query length: 200 chars · Max `filter_by`: 500 chars
- SQL/NoSQL injection patterns rejected at API boundary
- Prompt-injection signatures rejected for premium LLM endpoints
- RAG source types restricted to tenant-authorized allow-list

### Rate Limiting

| Endpoint | Limit | Window |
|---|---|---|
| `GET /api/v1/search` | 60 requests | per minute per API key |
| `GET /api/v1/suggest` | 120 requests | per minute per API key |
| `POST /api/v1/events` | 1,000 events | per minute per service |
| Admin endpoints | 30 requests | per minute per user |

Rate limit headers: `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`

### Data Protection

- No PII in the search index
- `user_id` in analytics events is a SHA-256 pseudonymous hash (never raw identity)
- Valkey cache keys never contain raw user data
- All data in transit: TLS 1.2+
- All data at rest: encrypted (cloud provider default)
- RAG outputs contain citations only from tenant-authorized sources

### GDPR Compliance

| Requirement | Implementation |
|---|---|
| Right to erasure | `DELETE /api/users/me/data` — clears Valkey profile + PostgreSQL signal rows |
| Erasure auditability | `data_erasure_log` table: `user_id_hash`, `scope`, `erased_at`, `requested_by` |
| Compliance SLA | Erasure complete within 30 calendar days |
| Data minimization | Signal partitions > 90 days pruned nightly |
| Consent revocation | Affinity refresh skipped for `consent_status = 'revoked'` |

---

## 14. Infrastructure Topology

```
Internet
    │
    ▼
AWS CloudFront (CDN + WAF)
    │
    ▼
AWS Application Load Balancer
    │
    ▼
Amazon EKS (nexora-prod namespace)
    ├── Search API           (HPA: min=2, max=10, scale on CPU 70% + P95 latency)
    ├── Admin Dashboard      (separate service)
    ├── Index Sync Service   (CDC consumer)
    ├── Stream Processor     (RabbitMQ consumer)
    ├── Embedding Service    (Phase 4)
    ├── LLM Understanding    (Phase 4, vLLM)
    ├── Recommendation Svc   (Phase 4.5)
    └── CronJobs:
        ├── ctr-signal-aggregator        (02:00 UTC daily)
        ├── conversion-signal-aggregator (02:30 UTC daily)
        ├── affinity-profile-refresher   (03:00 UTC daily)
        └── partition-pruner             (04:00 UTC daily)

Data Services:
    ├── Typesense Cluster (3-node: 1 leader + 2 replicas) — 4 vCPU / 8GB RAM per node
    ├── ElastiCache (Valkey 8)
    ├── AmazonMQ (RabbitMQ)
    ├── MSSQL Server (primary catalog + CDC)
    ├── RDS PostgreSQL Multi-AZ (metadata + signals + conversations)
    └── Vector DB — Qdrant / Weaviate / Milvus (Phase 4)

Supporting:
    ├── AWS Secrets Manager
    ├── AWS S3 (event archive, Parquet/Gzip)
    ├── AWS CloudWatch (metrics + logs + alarms)
    └── Amazon Athena (ad-hoc S3 analytics)
```

All infrastructure defined in Terraform / AWS CDK. No manual provisioning in production.

---

## 15. Performance Requirements

| Requirement | Target |
|---|---|
| Search API P95 latency | < 100ms |
| Search API P99 latency | < 200ms |
| Typeahead P95 latency | < 50ms |
| Typesense query time | < 20ms |
| Cache hit ratio | >= 40% (popular queries) |
| Throughput | Autoscale to 3x peak (EKS HPA) |
| Index freshness — stock | <= 5 min lag |
| Uptime SLA | 99.9% monthly |
| LLM query understanding P95 | < 200ms |
| AI summary generation P95 | < 500ms |
| Conversational response P95 | < 300ms |
| RAG answer generation P95 | < 700ms |
| Recommendation API P95 | < 80ms |

---

## 16. Observability

### Key Metrics (Prometheus / CloudWatch)

| Metric | Type |
|---|---|
| `search_requests_total` | Counter (endpoint, status_code) |
| `search_latency_ms` | Histogram (endpoint, cache_hit) |
| `search_cache_hits_total` | Counter (cache_type) |
| `search_typesense_latency_ms` | Histogram |
| `search_zero_results_total` | Counter |
| `search_index_sync_lag_seconds` | Gauge |
| `search_llm_latency_ms` | Histogram (model, operation) |
| `search_vector_latency_ms` | Histogram (index, mode) |
| `search_hybrid_requests_total` | Counter (mode, fallback_used) |
| `search_rag_latency_ms` | Histogram (model, retrieval_k) |
| `search_rag_low_confidence_total` | Counter (tenant_id) |
| `search_recommendation_latency_ms` | Histogram (context) |
| `search_recommendation_ctr` | Gauge (placement) |

### Distributed Tracing

OpenTelemetry traces propagated end-to-end through the full request chain:

```
[API] → [QueryProcessor] → [CacheCheck] → [Typesense] → [RankingEngine] → [Response]
```

Trace IDs propagated to analytics events for full correlation.

### Alert Thresholds

| Condition | Severity |
|---|---|
| P95 > 150ms for 5 min | Warning |
| P95 > 300ms for 2 min | Critical |
| HTTP 5xx > 1% for 5 min | Critical |
| Zero-result rate > 15% for 10 min | Warning |
| Cache hit ratio < 20% for 15 min | Warning |
| Stock sync lag > 10 min | Warning |
| LLM P95 > 250ms for 5 min | Warning |
| AI summary failure > 2% for 5 min | Critical |
| Hybrid fallback ratio > 15% for 10 min | Warning |
| RAG low-confidence > 10% for 10 min | Warning |
| Recommendation CTR drop > 20% WoW | Warning |

### Dashboards (Grafana)

- **Search Operations:** latency percentiles, QPS, error rate, cache hit ratio
- **Search Quality:** CTR, conversion, zero-result rate, top queries
- **Infrastructure:** Typesense node health, Valkey memory, K8s pod count
- **Phase 2 CronJobs:** last run time, duration, success/failure
- **Feature Flags:** enable/disable events, canary traffic %, rollback incidents

---

## 17. Feature Flags & Canary Rollout

### Feature Flags (Kubernetes ConfigMap)

```yaml
FEATURE_BEHAVIORAL_RANKING:  "true"   # CTR + conversion signals in FinalScore
FEATURE_PERSONALIZATION:     "true"   # Rule-based Valkey affinity profiles
FEATURE_AB_TESTING:          "true"   # A/B test framework active
FEATURE_ZERO_RESULT_FALLBACK:"true"   # Fuzzy fallback on zero results
```

All flags default to `false`. Rolling restart required for flag changes.

### Phase 2 Canary Sequence

| Stage | Traffic | Duration | Gate |
|---|---|---|---|
| Canary | 10% | 48h | CTR regression <= 5% |
| Early Majority | 25% | 48h | CTR regression <= 5%; P95 < 100ms |
| Majority | 50% | 48h | CTR regression <= 10%; error rate < 0.5% |
| Full Rollout | 100% | — | All gates passed |

Rollback trigger: if CTR regression > 10% at any stage, set `FEATURE_BEHAVIORAL_RANKING=false` + `FEATURE_PERSONALIZATION=false`. Rolling restart completes in < 2 minutes.

---

## 18. Disaster Recovery & High Availability

| Component | Strategy |
|---|---|
| Typesense | 3-node cluster (1 leader + 2 replicas); data replicated across nodes |
| Valkey | ElastiCache Multi-AZ with automatic failover |
| RDS PostgreSQL | Multi-AZ deployment with automatic failover |
| RabbitMQ (AmazonMQ) | Active/standby broker with automatic failover |
| EKS | Multi-AZ node groups; HPA min=2 ensures availability during rolling restarts |
| MSSQL | Existing HA setup (not managed by Nexora) |
| LLM Services | Self-hosted vLLM + API fallback circuit breaker (Phase 4) |
| Vector DB | Replication per Vector DB vendor configuration (Phase 4) |

**Recovery Objectives:**
- RTO: < 15 minutes for primary search path
- RPO: < 10 minutes for index data; < 24 hours for analytics signals

---

## 19. Rollout Phases

| Phase | Scope | Target Timeline |
|---|---|---|
| **Phase 1** | Core keyword search, typeahead, CDC sync, admin API | Weeks 1–7 |
| **Phase 2** | Analytics pipeline, behavioral ranking, A/B testing, personalization (rule-based), GDPR compliance | Weeks 8–14 |
| **Phase 3** | ML-based personalization, LightGBM/Two-Tower ranking model | Weeks 14–22 |
| **Phase 4** | Premium hybrid search, LLM query understanding, AI summaries, conversational search | Weeks 23–29 |
| **Phase 4.5** | RAG knowledge answers, AI recommendations, merchandising controls | Weeks 30–32 |

---

*For full requirements, see [docs/BRD.md](docs/BRD.md) (Business Requirements) and [docs/TRD.md](docs/TRD.md) (Technical Requirements).*
