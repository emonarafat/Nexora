# Technical Requirements Document (TRD)

**Project Name:** Enterprise Search & Discovery System  
**Platform:** E-commerce Marketplace  
**Search Engine:** Typesense  
**API Stack:** .NET 10 Minimal API — Vertical Slice Architecture  
**Version:** 1.2  
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
18. [Feature Flags & Canary Rollout](#18-feature-flags--canary-rollout)

---

## 1. Architecture Overview

### High-Level Components

```
┌───────────────────────────────────────────────────────────────┐
│              Product Source (MSSQL Server)                    │
│   Product catalog, stock levels, pricing, attributes          │
└────────────────────────┬──────────────────────────────────────┘
                         │ CDC / Batch Sync
┌────────────────────────▼──────────────────────────────────────┐
│             Index Sync Service (.NET 10 Worker)               │
│     Change-Data-Capture · Upsert · Full re-index scheduler    │
└────────────────────────┬──────────────────────────────────────┘
                         │ Typesense API
┌───────────────────────────────────────────────────────────────┐
│                        Client Layer                           │
│        Web App / Mobile App / Admin Dashboard                 │
└────────────────────────┬──────────────────────────────────────┘
                         │ HTTPS
┌────────────────────────▼──────────────────────────────────────┐
│                    API Gateway / BFF                           │
│              Rate Limiting · Auth · TLS                       │
└────────────────────────┬──────────────────────────────────────┘
                         │
┌────────────────────────▼──────────────────────────────────────┐
│               Search API (.NET 10 Minimal API)                 │
│                 Vertical Slice Architecture                    │
│  ┌────────────┐ ┌──────────────┐ ┌────────────────────────┐  │
│  │  /search   │ │  /suggest    │ │  /analytics (internal) │  │
│  └────────────┘ └──────────────┘ └────────────────────────┘  │
└────────────────────────┬──────────────────────────────────────┘
                         │
          ┌──────────────┼──────────────┐
          │              │              │
┌─────────▼──────┐ ┌─────▼──────┐ ┌───▼───────────┐
│  Query         │ │  Ranking   │ │  Personali-   │
│  Processor     │ │  Engine    │ │  zation Layer │
└─────────┬──────┘ └─────┬──────┘ └───────────────┘
          │              │
          │              │
┌─────────▼──────────────▼───────────┐   ┌─────────────────────────┐
│         Typesense Cluster          │   │   Vector DB (Qdrant /   │
│   (Keyword retrieval + faceting)   │   │ Weaviate / Milvus HNSW) │
└──────────────────────┬─────────────┘   └─────────────┬───────────┘
                       │                               │
                       └──────────────┬────────────────┘
                                      ▼
                          Hybrid Fusion + Re-ranker
                                      │
                          ┌───────────▼───────────┐
                          │ LLM Understanding Svc │
                          │ (intent, filters, NLP)│
                          └───────────────────────┘
          │
┌─────────▼──────────────────────────────────────────┐
│               Valkey Cache Layer                    │
│     (Frequent queries · Typeahead · User profiles)  │
└────────────────────────────────────────────────────┘
          │
┌─────────▼──────────────────────────────────────────┐
│               Analytics Pipeline                    │
│  Event Stream (RabbitMQ) → DWH                     │
└────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility |
|---|---|
| **Search API** | HTTP entrypoint, request validation, response shaping, auth |
| **Query Processor** | Normalization, spell correction, synonym expansion, intent detection |
| **Ranking Engine** | Score computation, signal blending, business rule application |
| **Personalization Layer** | User-affinity signals injection into ranking (Phase 2+) |
| **MSSQL Server** | Primary product catalog — source of truth for product data, stock, pricing |
| **Index Sync Service** | CDC listener + batch scheduler; maps MSSQL rows to Typesense documents |
| **Typesense Cluster** | Indexing, full-text search, faceting, typo tolerance |
| **Vector Database** | Semantic nearest-neighbor retrieval over product embeddings |
| **Embedding Service** | Generates query and product embeddings for semantic search |
| **LLM Understanding Service** | Intent detection, filter extraction, query expansion, summarization |
| **Knowledge Ingestion Pipeline** | Chunks/catalogs FAQs, manuals, policies for RAG retrieval |
| **Recommendation Service** | Computes and serves similar-item, bundle, and cross-sell suggestions |
| **Valkey Cache** | Query result caching, typeahead caching, rate limiting counters |
| **Analytics Pipeline** | Event ingestion, aggregation, KPI computation |
| **Admin Dashboard** | Synonym management, ranking weight tuning, zero-result queue |

---

## 2. System Flow

### Search Request Flow

```
1. Client sends: GET /api/search?q=runng+shoes&page=1&size=20
                                         
2. API Layer
   ├── Authenticate request (API key / JWT)
   ├── Validate and bind query parameters
   └── Route to Search feature slice

3. Query Processor
   ├── Normalize: lowercase, trim, strip special chars
   ├── Spell correction: "runng" → "running"
   ├── Synonym expansion: "shoes" → ["shoes", "footwear", "trainers"]
   ├── Intent classification: transactional (product search)
   └── Emit normalized query + signals

4. Cache Check
   ├── Hash normalized query + filters + page
   ├── HIT → return cached response (track cache hit)
   └── MISS → proceed to Typesense

5. Typesense Retrieval
   ├── Execute search with normalized query
   ├── Apply hard filters: stock_status = "in_stock" (if configured)
   ├── Apply facet aggregations
   └── Return candidate set (top N × ranking_multiplier)

6. Ranking Engine
   ├── Compute FinalScore per candidate (formula in §5)
   ├── Apply business boosts: featured, promotional
   ├── Apply demotions: out-of-stock, flagged items
   └── Sort by FinalScore descending

7. Personalization (Phase 2+)
   └── Adjust FinalScore using user affinity signals

8. Response Assembly
   ├── Paginate results
   ├── Attach facet buckets
   ├── Attach query metadata (corrected query, synonyms used)
   └── Serialize to JSON

9. Cache Write
   └── Store response in Valkey with TTL (configurable, default 60s)

10. Analytics Event
    └── Emit search_query event asynchronously (fire-and-forget)
```

### Typeahead Flow

```
1. Client sends: GET /api/suggest?q=run (debounced, 300ms)
2. Cache check (Valkey key: suggest::{normalized_prefix})
3. HIT → return immediately
4. MISS → Typesense instant search (prefix match, top 8 results)
5. Cache write (TTL: 30s)
6. Return suggestions
Target latency: < 50ms P95
```

### Premium Hybrid Search Flow (Phase 4)

```
1. Client sends: GET /api/v1/search?q=modern+sofa+small+space&mode=hybrid
2. Query Processor normalizes and enriches query
3. LLM Understanding extracts intent + implicit filters + expanded terms
4. Parallel retrieval:
  - Typesense keyword retrieval
  - Vector DB semantic retrieval using query embedding
5. Candidate merge + normalization of component scores
6. Hybrid ranking:
  HybridScore = (0.40 * KeywordScore) + (0.30 * SemanticScore) + (0.30 * MlScore)
7. Business boosts/demotions + personalization adjustments
8. Response assembly (optionally with summary/comparison hooks)
9. Emit premium analytics events (understanding latency, semantic hit quality)
```

### RAG Product Knowledge Flow (Phase 4.5)

```
1. Client sends: POST /api/v1/search/knowledge/ask
2. API validates tenant scope + safety filters
3. Query embedding generated
4. Vector DB retrieves top-k knowledge chunks (manuals, FAQs, policies)
5. Re-ranker reorders passages by relevance + source reliability
6. LLM generates grounded answer constrained to retrieved context
7. Response returns answer + citations + confidence score
8. If confidence below threshold, fallback to deterministic links and filters
```

### AI Recommendation Flow (Phase 4.5)

```
1. Client sends: GET /api/v1/recommendations?product_id=prod_123
2. Service loads candidate pools (similarity, co-purchase, session affinity)
3. Candidate blending applies stock, margin, and merchant constraints
4. RecommendationScore ranks top items
5. Response returns recommendations with reason codes
6. Events emitted for impression/click/conversion attribution
```

---

## 3. Technology Stack

| Layer | Technology | Rationale |
|---|---|---|
| API Framework | .NET 10 Minimal API | Low overhead, native AOT compatible, fast startup |
| Architecture Pattern | Vertical Slice Architecture | Feature isolation, no cross-cutting coupling |
| Search Engine | Typesense (self-hosted or Cloud) | Sub-10ms queries, typo tolerance built-in, low ops complexity vs Elastic |
| Cache | Valkey 8 | Sub-millisecond reads, TTL support, distributed (Redis-compatible OSS fork) |
| Message Bus | RabbitMQ | Analytics event delivery, durable queues, topic exchanges |
| Embedding Model | BAAI/bge-small-en-v1.5 (default) | 384-dim, cost-efficient, low latency |
| Vector Database | Qdrant / Weaviate / Milvus | ANN search (HNSW), horizontal scalability |
| LLM Runtime | vLLM (self-hosted) with API fallback | Low latency, cost control, graceful degradation |
| RAG Re-ranker | Cross-encoder reranker (e.g., bge-reranker-base) | Improves grounding precision for answer generation |
| Product Database | MSSQL Server | Primary product catalog — source of truth for all indexed data |
| Database (metadata) | PostgreSQL | Synonym dictionary, ranking overrides, A/B test config |
| Knowledge Store | S3 + metadata in PostgreSQL | Versioned knowledge chunks and citation metadata |
| Admin API | .NET 10 Minimal API (internal) | Consistent stack |
| Containerization | Docker + Amazon EKS | Horizontal scaling, zero-downtime deploys |
| CI/CD | GitHub Actions | Standard pipeline |
| Observability | OpenTelemetry → Grafana + Prometheus | Vendor-neutral tracing and metrics |
| APM | AWS CloudWatch | Latency percentiles, error tracking |

### Typesense vs Elasticsearch Decision

| Factor | Typesense | Elasticsearch |
|---|---|---|
| Ops complexity | Low (single binary) | High (JVM tuning, cluster management) |
| Latency at p95 | < 10ms typical | 20–100ms typical |
| Typo tolerance | Built-in, configurable | Requires fuzzy query config |
| Cost (self-hosted) | Low | Medium–High (JVM memory) |
| Ecosystem maturity | Moderate | Very mature |
| ML ranking | Not native (handled externally) | Learning to Rank plugin available |

**Decision:** Typesense for phases 1–2. Re-evaluate at phase 3 if ML ranking complexity demands Elasticsearch Learning to Rank.

---

## 4. Index Design

### Collection: `products`

```json
{
  "name": "products",
  "fields": [
    { "name": "id",               "type": "string",  "facet": false },
    { "name": "title",            "type": "string",  "facet": false, "weight": 10 },
    { "name": "brand",            "type": "string",  "facet": true,  "weight": 5  },
    { "name": "sku",              "type": "string",  "facet": false                },
    { "name": "description",      "type": "string",  "facet": false, "weight": 2  },
    { "name": "category",         "type": "string",  "facet": true                },
    { "name": "category_path",    "type": "string[]","facet": true                },
    { "name": "price",            "type": "float",   "facet": true,  "sort": true },
    { "name": "currency",         "type": "string",  "facet": false               },
    { "name": "color",            "type": "string[]","facet": true                },
    { "name": "size",             "type": "string[]","facet": true                },
    { "name": "attributes",       "type": "string[]","facet": true                },
    { "name": "rating",           "type": "float",   "facet": false, "sort": true },
    { "name": "rating_count",     "type": "int32",   "facet": false               },
    { "name": "stock_status",     "type": "string",  "facet": true                },
    { "name": "stock_quantity",   "type": "int32",   "facet": false               },
    { "name": "popularity_score", "type": "float",   "facet": false, "sort": true },
    { "name": "ctr_30d",          "type": "float",   "facet": false               },
    { "name": "conversion_rate_30d", "type": "float","facet": false               },
    { "name": "is_featured",      "type": "bool",    "facet": false               },
    { "name": "is_active",        "type": "bool",    "facet": false               },
    { "name": "merchant_id",      "type": "string",  "facet": true                },
    { "name": "created_at",       "type": "int64",   "facet": false, "sort": true },
    { "name": "updated_at",       "type": "int64",   "facet": false               }
  ],
  "default_sorting_field": "popularity_score",
  "token_separators": ["-", "/"],
  "symbols_to_index": ["+", "#"]
}
```

### Collection: `knowledge_chunks` (RAG)

```json
{
  "name": "knowledge_chunks",
  "fields": [
    { "name": "chunk_id", "type": "string" },
    { "name": "merchant_id", "type": "string", "facet": true },
    { "name": "source_type", "type": "string", "facet": true },
    { "name": "source_id", "type": "string" },
    { "name": "content", "type": "string" },
    { "name": "embedding", "type": "float[]" },
    { "name": "language", "type": "string", "facet": true },
    { "name": "updated_at", "type": "int64", "sort": true }
  ]
}
```

### Index Update Strategy

| Trigger | Method | Latency SLA |
|---|---|---|
| Product created / updated | CDC on MSSQL (`products` table) → Index Sync Service upsert | < 30 seconds |
| Stock status change | CDC on MSSQL (`stock` table) → partial update (stock fields only) | < 5 minutes |
| Price change | CDC on MSSQL (`pricing` table) → partial update (price fields only) | < 5 minutes |
| CTR / conversion signals | Batch update (hourly) | 1 hour |
| Full re-index | Scheduled (weekly, off-peak); reads full MSSQL product view | < 2 hours |

### Multi-tenancy

- `merchant_id` field enables per-merchant filtering
- Search API enforces `merchant_id` scope based on auth context for merchant-facing endpoints
- Marketplace-wide search applies no merchant filter

---

## 5. Product Data Ingestion

### Source of Truth

All product data originates from **MSSQL Server**. The **Index Sync Service** (a .NET 10 `BackgroundService` / Worker) is the exclusive bridge between MSSQL and Typesense.

### Change-Data-Capture (Real-Time Sync)

MSSQL Server's built-in **Change Data Capture (CDC)** is enabled on the following tables:

| MSSQL Table | CDC Columns Tracked | Typesense Fields Updated |
|---|---|---|
| `products` | All columns | Full document upsert |
| `stock` | `stock_status`, `stock_quantity` | `stock_status`, `stock_quantity` |
| `pricing` | `price`, `currency` | `price`, `currency` |

The Index Sync Service polls the CDC change tables via `cdc.fn_cdc_get_all_changes_*` every **10 seconds** and pushes batched upserts to Typesense using the bulk import endpoint.

### Batch / Full Re-index

```
MSSQL View: vw_search_product_flat
  JOIN products + stock + pricing + attributes + categories
  WHERE is_active = 1
```

Full re-index reads `vw_search_product_flat` in pages of **1,000 rows** and calls the Typesense bulk import API. Runs weekly off-peak (Sunday 02:00 UTC). Can also be triggered manually via Admin API.

### Knowledge Ingestion (Phase 4.5)

- Sources: product manuals, size guides, shipping/return policies, merchant FAQs
- Processing: extract text -> chunk (300-500 tokens) -> embed -> upsert to `knowledge_chunks`
- Freshness SLA: source updates reflected in retrieval index within 30 minutes
- PII safety: ingestion pipeline rejects sensitive fields by schema allow-list

### Field Mapping

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

### Error Handling

- Failed upserts are retried with exponential back-off (max 5 attempts)
- Poison records are written to a `sync_dead_letter` table in MSSQL with error details
- Sync lag is exposed as a Prometheus gauge: `search_index_sync_lag_seconds`
- Alert fires if lag exceeds **5 minutes** (stock) or **60 seconds** (product create/update)

---

## 6. Ranking Strategy

### Base Relevance — BM25

Typesense uses BM25 as its default text relevance scoring:

$$\text{BM25}(q, d) = \sum_{t \in q} \text{IDF}(t) \cdot \frac{f(t,d) \cdot (k_1 + 1)}{f(t,d) + k_1 \cdot \left(1 - b + b \cdot \frac{|d|}{\text{avgdl}}\right)}$$

Where:
- $f(t,d)$ = term frequency in document $d$
- $\text{IDF}(t)$ = inverse document frequency of term $t$
- $k_1 = 1.2$ (term saturation, default)
- $b = 0.75$ (length normalization, default)
- $|d|$ = document length; $\text{avgdl}$ = average document length

### Field Weights

```json
"query_by": "title,brand,sku,description,category",
"query_by_weights": "10,5,4,2,3"
```

### Final Ranking Formula

All signal values are normalized to [0, 1] before blending.

```
FinalScore =
  (0.40 × TextScore)      // BM25 normalized relevance
  (0.20 × CTR_30d)        // Click-through rate (30-day rolling)
  (0.15 × ConversionRate) // Purchase conversion rate
  (0.10 × Availability)   // 1.0 if in_stock, 0.0 if out_of_stock
  (0.10 × Rating)         // Normalized avg rating (0–5 → 0–1)
  (0.05 × Personalization) // Phase 2: user affinity signal
```

**Sum of weights = 1.0**

> These weights are configurable at runtime via the Admin API. Defaults are stored in PostgreSQL and loaded at startup with a 60-second refresh interval.

### Boosts & Demotions

| Condition | Action | Magnitude |
|---|---|---|
| `is_featured = true` | Multiply FinalScore | × 1.3 |
| `stock_status = "low_stock"` | Demote | × 0.9 |
| `stock_status = "out_of_stock"` | Demote or filter | × 0.0 (or excluded) |
| `rating < 2.5 AND rating_count > 20` | Demote | × 0.7 |
| Promotional campaign active | Admin-injected boost | Configurable (1.1–2.0) |

### Phase 4 Hybrid Formula

For premium mode (`search_mode=hybrid`), ranking switches to:

```
HybridScore =
  (0.40 * NormalizedKeywordScore)
  + (0.30 * NormalizedSemanticScore)
  + (0.30 * NormalizedPhase3MlScore)
```

Optional multipliers:
- High semantic confidence boost: x1.25 when semantic score > 0.85
- Poor keyword confidence dampening: x0.15 when keyword score < 0.30
- Agreement boost: x1.15 when keyword + semantic both rank item in top decile

### Recommendation Ranking Formula (Phase 4.5)

```
RecommendationScore =
  (0.35 * SimilarityScore)
  + (0.30 * CoPurchaseScore)
  + (0.20 * SessionAffinityScore)
  + (0.10 * MarginScore)
  + (0.05 * AvailabilityScore)
```

Business constraints:
- Exclude out-of-stock items
- Cap repetitive brand/category repetition in top-N
- Apply merchant blocklist/pinlist overrides after scoring

### Sort Modes

Clients may request explicit sort modes that override ranking:

| Sort Mode | Typesense Sort Parameter |
|---|---|
| `relevance` (default) | `_text_match:desc,popularity_score:desc` |
| `price_asc` | `price:asc` |
| `price_desc` | `price:desc` |
| `rating` | `rating:desc,rating_count:desc` |
| `newest` | `created_at:desc` |

---

## 7. Query Processing Pipeline

### Pipeline Stages

```
Input query string
       │
       ▼
1. Sanitization
   - Trim whitespace
   - Strip control characters and HTML
   - Truncate to max 200 chars
       │
       ▼
2. Normalization
   - Lowercase
   - Unicode NFC normalization
   - Remove excessive punctuation
       │
       ▼
3. Spell Correction
   - Check against Typesense built-in typo tolerance
   - configurable: num_typos = 1 (queries ≤ 8 chars), 2 (longer)
   - Return corrected_query to client in response metadata
       │
       ▼
4. Synonym Expansion
   - Load synonym map from PostgreSQL (in-memory cache, 5-min TTL)
   - Expand synonyms: "sofa" → ["sofa", "couch", "settee"]
   - Both one-way and two-way synonym types supported
       │
       ▼
5. Intent Classification
   - Rule-based classifier (Phase 1):
     • SKU pattern → navigational (exact match mode)
     • Brand name detected → brand-boosted mode
     • Category term detected → category-filtered mode
     • Default → transactional
   - ML classifier (Phase 3): replace rule-based
       │
       ▼
6. Query Structuring
   - Produce final Typesense search parameters
   - Inject synonyms, filters, sort, pagination
       │
       ▼
7. LLM Enrichment (Premium Mode)
  - Intent refinement: navigational / transactional / informational / comparative / search-then-filter
  - Implicit filter extraction to structured JSON
  - Expansion terms injected into retrieval query

8. Safety Guardrails (Premium Mode)
  - Prompt-injection and jailbreak pattern checks on input query
  - Output validation to enforce JSON schema for extracted filters
  - Tenant-scope validation before retrieval/generation

Output: Retrieval request object(s) for keyword-only or hybrid search
```

### Typo Tolerance Configuration

```json
{
  "num_typos": 2,
  "min_len_1typo": 4,
  "min_len_2typo": 8,
  "typo_tokens_threshold": 1,
  "drop_tokens_threshold": 1
}
```

---

## 8. Filtering & Faceting

### Supported Filter Operations

| Filter | Type | Operation |
|---|---|---|
| `price` | float | range: `price:[10..250]` |
| `brand` | string | exact: `brand:=Nike` or multi: `brand:=[Nike,Adidas]` |
| `category` | string | exact: `category:=Footwear` |
| `color` | string[] | contains: `color:=[Red,Blue]` |
| `size` | string[] | contains: `size:=XL` |
| `rating` | float | gte: `rating:>=4` |
| `stock_status` | string | exact: `stock_status:=in_stock` |
| `merchant_id` | string | exact (server-injected, not client-exposed) |

### Facet Configuration

```json
"facet_by": "brand,category,price,color,size,rating,stock_status",
"max_facet_values": 20,
"facet_query": ""
```

Facet counts are returned per search response and used by the client to render filter UI. Facets are hidden when count = 0 to prevent dead-end filters.

### Pagination

| Parameter | Default | Max |
|---|---|---|
| `page` | 1 | — |
| `per_page` | 20 | 100 |

Deep pagination (page > 50) is rate-limited and logged as a potential scraping signal.

---

## 9. Personalization Engine

### Phase 2 — Rule-Based

User profile signals (sourced from User Service):

| Signal | Weight | Decay |
|---|---|---|
| Recent category views (7-day) | 0.4 | Linear decay over 7 days |
| Purchased brands (90-day) | 0.35 | No decay |
| Price range of recent sessions | 0.25 | 24-hour window |

Personalization score is computed per candidate in the ranking engine:

```
PersonalizationScore = 
  (0.4 × category_affinity_score)
  + (0.35 × brand_affinity_score)
  + (0.25 × price_affinity_score)
```

**Affinity Profile Storage (Valkey)**

- Cache key format: `user:affinity:{user_id_hash}` (SHA-256 of pseudonymous user ID)
- TTL: **90 days** (refreshed on every affinity update)
- Max affinity boost cap: **0.20** (prevents over-personalization for any single signal)
- Eviction policy: `allkeys-lru` (Valkey-level; profiles evicted under memory pressure)
- Profile fetch adds < 5ms to total request latency

**PersonalizationService** (`src/Search.Api/Infrastructure/Personalization/PersonalizationService.cs`)

```csharp
public async Task<PersonalizationProfile?> GetProfileAsync(string userId, CancellationToken ct)
{
    var key = $"user:affinity:{ComputeHash(userId)}";
    return await _valkey.GetAsync<PersonalizationProfile>(key, ct);
}
```

**Affinity Refresh CronJob**

- Runs daily at **03:00 UTC** in `nexora-prod` namespace
- Reads last 90 days of `search_ctr_signals` and `search_conversion_signals` from PostgreSQL
- Recomputes affinity scores and writes updated profiles to Valkey (renewing 90-day TTL)

**Opt-Out Endpoint**

`DELETE /api/users/me/affinity`
- Clears the Valkey affinity profile for the authenticated user
- Writes an erasure record to `data_erasure_log` with timestamp and `scope=affinity`
- Returns `204 No Content`

### Phase 3 — ML-Based

- Model: Gradient Boosted Trees (LightGBM) or Two-Tower Neural Net
- Training cadence: weekly batch retrain on 90-day click/purchase data
- Serving: embedded score lookup via precomputed user-item affinity table
- Fallback: rule-based scoring if ML inference fails or user profile unavailable

### Phase 4 — Semantic + Conversational Personalization

- Inject conversation/session context into personalization features when available
- Use semantic similarity to improve recall for long-tail and natural-language queries
- Conversation state persisted in PostgreSQL (`search_conversations`, `conversation_turns`)
- If LLM or vector retrieval is unavailable, system degrades to Phase 3 ML ranking automatically

### Recommendation Personalization Signals (Phase 4.5)

- Recent viewed-product embeddings (7-day decay)
- Repeat purchase category vectors (90-day window)
- Cart co-occurrence graph weights
- Merchant-configured strategic category boosts

### Anonymous Users

Anonymous users (no session profile) receive personalization weight = 0. The 0.05 weight is redistributed to TextScore (making it 0.45) for anonymous requests.

---

## 10. Analytics Pipeline

### Events Schema

All events are published to the message bus as CloudEvents v1.0-compliant JSON.

#### `search_query` Event

```json
{
  "specversion": "1.0",
  "type": "search.query",
  "source": "/api/search",
  "id": "uuid",
  "time": "2026-05-07T10:00:00Z",
  "data": {
    "session_id": "string",
    "user_id": "string | null",
    "query": "string",
    "corrected_query": "string | null",
    "result_count": 42,
    "filters_applied": ["brand:=Nike"],
    "page": 1,
    "latency_ms": 35
  }
}
```

#### `search_click` Event

```json
{
  "type": "search.click",
  "data": {
    "session_id": "string",
    "user_id": "string | null",
    "query": "string",
    "product_id": "string",
    "position": 3,
    "latency_ms": 0
  }
}
```

#### `search_add_to_cart` Event

```json
{
  "type": "search.add_to_cart",
  "data": {
    "session_id": "string",
    "user_id": "string | null",
    "query": "string",
    "product_id": "string",
    "position": 3
  }
}
```

#### `search_purchase` Event

```json
{
  "type": "search.purchase",
  "data": {
    "session_id": "string",
    "user_id": "string | null",
    "query": "string",
    "product_id": "string",
    "order_id": "string",
    "revenue": 79.99
  }
}
```

#### `search_ai_understanding` Event (Phase 4)

```json
{
  "type": "search.ai_understanding",
  "data": {
    "session_id": "string",
    "query": "string",
    "intent": "transactional",
    "intent_confidence": 0.91,
    "filters_extracted": { "price_max": 500, "style": "modern" },
    "latency_ms": 142
  }
}
```

#### `search_ai_summary` Event (Phase 4)

```json
{
  "type": "search.ai_summary",
  "data": {
    "session_id": "string",
    "user_id": "string | null",
    "product_id": "string",
    "summary_served": true,
    "latency_ms": 360
  }
}
```

#### `search_rag_answer` Event (Phase 4.5)

```json
{
  "type": "search.rag_answer",
  "data": {
    "session_id": "string",
    "query": "string",
    "citations_count": 3,
    "confidence": 0.88,
    "latency_ms": 420
  }
}
```

#### `search_recommendation_impression` Event (Phase 4.5)

```json
{
  "type": "search.recommendation_impression",
  "data": {
    "session_id": "string",
    "context": "product_detail",
    "source_product_id": "prod_123",
    "recommended_product_ids": ["prod_456", "prod_789"],
    "latency_ms": 48
  }
}
```

### Pipeline Architecture

```
Search API (fire-and-forget publish)
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
(DWH: BigQuery / Synapse)       (Data Lake: ADLS Gen2 / S3)
  │
  ▼
Analytics Dashboard (Power BI / Metabase / Grafana)
```

### KPI Computations

| KPI | Computation |
|---|---|
| CTR | `COUNT(search_click) / COUNT(search_query)` per query |
| Conversion Rate | `COUNT(search_purchase) / COUNT(search_query)` per query |
| Zero-result rate | `COUNT(search_query WHERE result_count = 0) / COUNT(search_query)` |
| Avg latency | `AVG(latency_ms)` from search_query events |

### Data Retention Policy

#### S3 Event Archive

Raw events are archived from RabbitMQ via the Stream Processor to S3 in **Parquet/Gzip** format, partitioned by `event_type/year/month/day`.

| Storage Class | Transition |
|---|---|
| Standard | Days 0–30 |
| Intelligent-Tiering | Day 30 |
| Glacier | Day 90 |
| Expire (delete) | Day 365 |

Athena is used for ad-hoc analysis over archived Parquet data.

#### PostgreSQL Signal Tables

`search_ctr_signals` and `search_conversion_signals` are **monthly-partitioned** (declarative partitioning on `event_at`). Partitions older than **90 days** are detached and dropped by a nightly CronJob.

#### Partition Pruning CronJob

Runs at **04:00 UTC** daily in `nexora-prod` namespace:

```sql
-- Drop partitions older than 90 days
DROP TABLE IF EXISTS search_ctr_signals_YYYY_MM;
DROP TABLE IF EXISTS search_conversion_signals_YYYY_MM;
```

#### GDPR Erasure

`DELETE /api/users/me/data` — Full erasure endpoint:

1. Clears Valkey affinity profile (`user:affinity:{hash}`)
2. Deletes rows from `search_ctr_signals` and `search_conversion_signals` where `user_id_hash = ?`
3. Writes audit record to `data_erasure_log`:

```sql
CREATE TABLE data_erasure_log (
  id            BIGSERIAL PRIMARY KEY,
  user_id_hash  TEXT        NOT NULL,
  scope         TEXT        NOT NULL,  -- 'affinity' | 'signals' | 'full'
  erased_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  requested_by  TEXT        NOT NULL   -- 'user' | 'admin' | 'gdpr_automation'
);
```

Compliance SLA: erasure complete within **30 calendar days** of request.

---

## 11. API Contract

### Search Endpoint

```
GET /api/v1/search
```

#### Query Parameters

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `q` | string | Yes | — | Search query, max 200 chars |
| `page` | int | No | 1 | Page number |
| `per_page` | int | No | 20 | Results per page (max 100) |
| `sort` | string | No | `relevance` | Sort mode: relevance, price_asc, price_desc, rating, newest |
| `filter_by` | string | No | — | Typesense filter expression |
| `facet_by` | string | No | all | Comma-separated facet fields |

#### Response Schema

```json
{
  "query": "running shoes",
  "corrected_query": "running shoes",
  "total_results": 284,
  "page": 1,
  "per_page": 20,
  "latency_ms": 32,
  "results": [
    {
      "id": "prod_123",
      "title": "Nike Air Zoom Running Shoes",
      "brand": "Nike",
      "price": 89.99,
      "currency": "USD",
      "rating": 4.7,
      "rating_count": 1200,
      "stock_status": "in_stock",
      "image_url": "https://cdn.example.com/prod_123.jpg",
      "category": "Footwear > Running",
      "score": 0.893
    }
  ],
  "facets": {
    "brand": [
      { "value": "Nike", "count": 84 },
      { "value": "Adidas", "count": 71 }
    ],
    "price": [
      { "min": 0, "max": 50, "count": 45 },
      { "min": 50, "max": 150, "count": 162 }
    ]
  }
}
```

### Typeahead Endpoint

```
GET /api/v1/suggest?q={prefix}
```

### Premium Search Extensions (Phase 4)

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/v1/search/summary` | Generate AI summary for a product in current search context |
| `POST` | `/api/v1/search/compare` | Generate AI comparison insights for selected products |
| `POST` | `/api/v1/search/chat` | Continue conversational search turn and return refined results |

### Premium Expansion Extensions (Phase 4.5)

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/v1/search/knowledge/ask` | RAG answer generation with citations from product knowledge base |
| `GET` | `/api/v1/recommendations` | Returns similar/cross-sell/bundle recommendations with reason codes |
```

#### Response

```json
{
  "suggestions": [
    { "text": "running shoes", "category": "Footwear" },
    { "text": "running shorts", "category": "Apparel" },
    { "text": "running watch", "category": "Electronics" }
  ]
}
```

### Analytics Event Endpoint (Internal)

```
POST /api/v1/events
Authorization: Internal-Service-Key
```

```json
{
  "type": "search.click",
  "session_id": "string",
  "user_id": "string | null",
  "query": "string",
  "product_id": "string",
  "position": 3
}
```

### Admin Endpoints (Internal, Authenticated)

| Method | Path | Description |
|---|---|---|
| `GET` | `/admin/v1/synonyms` | List all synonyms |
| `POST` | `/admin/v1/synonyms` | Create synonym rule |
| `DELETE` | `/admin/v1/synonyms/{id}` | Delete synonym rule |
| `GET` | `/admin/v1/ranking-config` | Get current ranking weights |
| `PUT` | `/admin/v1/ranking-config` | Update ranking weights |
| `GET` | `/admin/v1/zero-result-queries` | List zero-result queries (7-day) |
| `GET` | `/admin/v1/ab-tests` | List A/B test configurations |
| `POST` | `/admin/v1/ab-tests` | Create A/B test |

---

## 12. Performance Requirements

| Requirement | Target | Measurement |
|---|---|---|
| Search API P95 latency | < 100ms | APM percentile metric |
| Search API P99 latency | < 200ms | APM percentile metric |
| Typeahead P95 latency | < 50ms | APM percentile metric |
| Typesense query time | < 20ms | Typesense metrics endpoint |
| Cache hit ratio | ≥ 40% for popular queries | Valkey INFO stats |
| Throughput | Autoscale to 3× peak (K8s HPA) | Load test validation |
| Index freshness (stock) | ≤ 5 minutes lag | Monitoring alert |
| Uptime SLA | 99.9% monthly | Uptime monitoring |
| LLM query understanding P95 | < 200ms | Model serving metrics |
| AI summary generation P95 | < 500ms | API + tracing metrics |
| Premium conversational response P95 | < 300ms | End-to-end trace metrics |
| RAG answer generation P95 | < 700ms | API + model + retrieval metrics |
| Recommendation API P95 | < 80ms | Service latency metrics |

### Caching Strategy

| Cache Key Pattern | TTL | Invalidation |
|---|---|---|
| `search::{query_hash}::{filters_hash}::{page}` | 60s | Explicit purge on index update |
| `suggest::{prefix_hash}` | 30s | Time-based only |
| `user_profile::{user_id}` | 600s | Explicit purge on profile update |
| `synonyms` | 300s | Explicit purge on admin update |
| `ranking_config` | 60s | Explicit purge on admin update |
| `llm_understanding::{query_hash}` | 30 days | Explicit purge on prompt/model changes |
| `summary::{product_id}::{context_hash}` | 7 days | Explicit purge on product detail update |
| `rag::{merchant_id}::{query_hash}` | 24h | Purge on knowledge source update |
| `reco::{context_hash}::{user_or_session}` | 15m | Purge on stock or merchandising policy updates |

### Frontend Performance Requirements

- Query debounce: 300ms before triggering typeahead
- Typeahead: show suggestions only after 2+ characters
- Search result skeleton UI shown within 100ms of submit
- Infinite scroll or pagination: load next page on scroll threshold or explicit click

---

## 13. Optimization Loop

### Weekly Search Review Cadence

Responsible: Search Analyst + Engineering

**Step 1 — Zero-result query review**
- Pull from `admin/zero-result-queries` (weekly batch)
- For each query with > 50 occurrences: add synonym, adjust category mapping, or create manual override

**Step 2 — Low-CTR query review**
- Queries with CTR < 5% and > 100 impressions
- Review result quality manually
- Adjust ranking weights or add boosting rules

**Step 3 — Ranking weight validation**
- Compare A/B test results if active
- Update FinalScore weights via Admin API
- Document rationale in ranking changelog

**Step 4 — Synonym dictionary maintenance**
- Review user search patterns for synonym opportunities
- Clean up conflicting or stale synonym rules

**Step 5 — KPI trend review**
- Compare week-over-week: CTR, conversion, latency, zero-result rate
- Escalate if any metric degrades > 10% WoW

**Step 6 — RAG quality and freshness review (Phase 4.5)**
- Review low-confidence answers and missing-citation incidents
- Validate source freshness SLA and retraining triggers

**Step 7 — Recommendation quality review (Phase 4.5)**
- Track recommendation CTR, attach rate, and downstream conversion
- Tune blending weights and merchandising constraints

---

## 14. Security Requirements

### Authentication & Authorization

| Surface | Auth Method |
|---|---|
| Public Search API | API key (per-client) passed via `X-API-Key` header |
| Admin API | JWT (short-lived, 1h expiry) via internal IdP |
| Analytics Event API | Internal service identity + mutual TLS |
| Typesense admin | Scoped API keys; never exposed outside cluster |

### Input Validation

- All query parameters validated against strict allow-list schemas (no passthrough to Typesense)
- `filter_by` parameter: parse and validate before forwarding — reject any expression referencing `merchant_id` (server-injected only)
- Max query length: 200 characters
- Max `filter_by` length: 500 characters
- Reject queries containing SQL/NoSQL injection patterns at API boundary
- Enforce allow-list for RAG source types and tenant ownership before retrieval
- Reject prompt-injection signatures for premium LLM endpoints

### Rate Limiting

| Endpoint | Limit | Window |
|---|---|---|
| `GET /api/v1/search` | 60 requests | per minute per API key |
| `GET /api/v1/suggest` | 120 requests | per minute per API key |
| `POST /api/v1/events` | 1000 events | per minute per service |
| Admin endpoints | 30 requests | per minute per user |

Rate limit headers returned on all responses: `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`.

### Data Protection

- No PII stored in search index
- `user_id` in analytics events is a pseudonymous identifier (hashed external ID)
- Valkey cache keys never contain raw user data
- All data in transit: TLS 1.2+
- All data at rest: encrypted (cloud provider default encryption)
- RAG outputs include citations only from tenant-authorized sources

### GDPR & PII Compliance (Phase 2)

| Requirement | Implementation |
|---|---|
| Right to erasure | `DELETE /api/users/me/data` — clears Valkey profile + PostgreSQL signal rows |
| Right to erasure (affinity only) | `DELETE /api/users/me/affinity` — clears Valkey profile only |
| Erasure auditability | `data_erasure_log` table: user_id_hash, scope, erased_at, requested_by |
| Compliance SLA | Erasure complete within 30 calendar days |
| PII in signals | `user_id` stored only as SHA-256 hash; never raw identity |
| Data minimization | CronJob prunes signal partitions > 90 days at 04:00 UTC daily |
| Consent revocation | Affinity refresh skipped for users with consent_status = 'revoked' |

### Dependency Security

- Typesense API key rotation: quarterly minimum
- Valkey AUTH enabled; no open access
- Container images: scanned on every CI build (Trivy / Snyk)
- Dependencies: renovate-bot for automated patch updates

---

## 15. Infrastructure & Cost Optimization

### Typesense Deployment

- **Production:** 3-node cluster (1 leader + 2 replicas) for HA
- **Staging:** Single node
- **Index size estimate:** ~500 bytes per product × 1M products = ~500MB (well within Typesense RAM limits)
- **Node sizing:** 4 vCPU / 8GB RAM per node (re-evaluate at 5M+ products)

### Cost Drivers & Controls

| Cost Driver | Control |
|---|---|
| Typesense query volume | Valkey cache reduces Typesense load by ≥ 40% |
| Valkey memory | Eviction policy: `allkeys-lru`; monitor memory usage weekly |
| Analytics event storage | Raw events retained 90 days in PostgreSQL; archived to S3 (Parquet/Gzip); expire Day 365 |
| Kubernetes pods | HPA min=2, max=10; scale on CPU 70% + latency p95 |
| LLM inference spend | Self-hosted baseline + API fallback circuit breaker + per-request budget cap |
| Vector DB memory growth | Product embedding compaction, namespace lifecycle policy, cold-tier migration |
| RAG ingestion compute | Incremental ingestion windows + change-only embedding regeneration |
| Recommendation compute | Precomputed candidate pools + online lightweight reranking |

### Phase 2 CronJobs

All CronJobs run in the `nexora-prod` Kubernetes namespace:

| Job | Schedule (UTC) | Responsibility |
|---|---|---|
| `ctr-signal-aggregator` | 02:00 daily | Reads `search_click` events from S3/RabbitMQ; writes to `search_ctr_signals` |
| `conversion-signal-aggregator` | 02:30 daily | Reads `search_purchase` events; writes to `search_conversion_signals` |
| `affinity-profile-refresher` | 03:00 daily | Recomputes Valkey affinity profiles from 90-day signal window |
| `partition-pruner` | 04:00 daily | Detaches + drops PostgreSQL signal partitions older than 90 days |

### Infrastructure as Code

- All infrastructure defined in Terraform / AWS CDK
- No manual provisioning in production
- Secrets managed via AWS Secrets Manager (never in code or environment variables)

---

## 16. Testing Strategy

### Unit Tests

- Query processor pipeline (each stage independently)
- Ranking formula computation
- Filter expression parser and validator
- Cache key generation

### Integration Tests

- Search API → Typesense (docker-compose test environment with seeded index)
- Admin API → PostgreSQL synonym management
- Event publishing → message bus (test topic)

### Phase 2 Integration & E2E Test Suite

**Container Stack (Testcontainers for .NET)**

| Service | Container Image |
|---|---|
| PostgreSQL | `postgres:16-alpine` — signal tables + migrations |
| RabbitMQ | `rabbitmq:3-management-alpine` — `search-events` exchange, DLQ |
| Typesense | `typesense/typesense:27` — seeded with 1000 products |
| Valkey | `valkey/valkey:8-alpine` — affinity profile caching |
| LocalStack | `localstack/localstack:3` — S3 bucket for event archive |

**Test Factory Classes**

```csharp
// Generates realistic search event payloads
SearchEventFactory.CreateClickEvent(sessionId, userId, query, productId, position)
SearchEventFactory.CreatePurchaseEvent(sessionId, userId, query, productId, orderId, revenue)

// Generates signal table rows for PostgreSQL seeding
SignalDataFactory.CreateCtrSignals(count: 5000, daysBack: 90)
SignalDataFactory.CreateConversionSignals(count: 1000, daysBack: 90)
```

**Integration Test Suites**

| Suite | Scenarios |
|---|---|
| `AnalyticsPipelineTests` | Event published → RabbitMQ → consumed → PostgreSQL row inserted |
| `PersonalizationServiceTests` | Affinity profile written to Valkey; cached on repeat fetch; opt-out clears cache |
| `DataRetentionTests` | Partition pruner removes partitions > 90 days; `data_erasure_log` row written |
| `AbTestingFrameworkTests` | User assigned to variant; result recorded; significance computed |
| `GdprErasureTests` | Full erasure endpoint clears Valkey + signal rows + writes audit log |

**k6 Load Test (Event Pipeline)**

```javascript
// Target: ≥ 1000 events/sec sustained throughput
export const options = {
  stages: [
    { duration: '1m', target: 500 },
    { duration: '3m', target: 1000 },
    { duration: '1m', target: 0 }
  ],
  thresholds: {
    http_req_duration: ['p95<200'],
    http_req_failed: ['rate<0.01']
  }
};
```

### Contract Tests

- API response schema validated against OpenAPI spec on every CI run
- Typesense collection schema validated against code definition

### Performance Tests

- Load test with k6: ramp to 3× expected peak QPS
- Verify P95 latency remains < 100ms under load
- Run weekly in staging environment

### A/B Test Validation

- Statistical significance threshold: 95% confidence
- Minimum experiment duration: 2 weeks
- Minimum traffic per variant: 5,000 searches
- Premium experiment target uplift: >= 35% conversion vs Phase 3 baseline

### Phase 4 Quality Validation

- Offline semantic relevance benchmark with curated query-product pairs
- Intent classification accuracy target: >= 85%
- Hallucination guard tests for summaries/comparisons (must stay attribute-grounded)
- Degradation tests: verify automatic fallback to non-AI path when vector or LLM dependency fails

### Phase 4.5 Expansion Validation

- RAG grounding tests: each answer must include at least one valid citation reference
- RAG freshness tests: modified policy source reflected within freshness SLA
- Recommendation offline evaluation: Precision@K, Recall@K, NDCG@K
- Recommendation online validation: CTR uplift and attach-rate uplift in A/B test

### Acceptance Criteria (Definition of Done)

| Criterion | Threshold |
|---|---|
| Unit test coverage | ≥ 80% line coverage |
| Integration tests | All critical paths covered |
| P95 latency (load test) | < 100ms at 2× peak |
| Zero security findings | OWASP Top 10 scan clean |
| Zero-result rate (staging) | Matches or improves baseline |

---

## 17. Observability

### Metrics (Prometheus / AWS CloudWatch)

| Metric | Type | Labels |
|---|---|---|
| `search_requests_total` | Counter | endpoint, status_code |
| `search_latency_ms` | Histogram | endpoint, cache_hit |
| `search_cache_hits_total` | Counter | cache_type |
| `search_typesense_latency_ms` | Histogram | — |
| `search_zero_results_total` | Counter | — |
| `search_ranking_score_avg` | Gauge | — |
| `search_llm_latency_ms` | Histogram | model, operation |
| `search_vector_latency_ms` | Histogram | index, mode |
| `search_hybrid_requests_total` | Counter | mode, fallback_used |
| `search_summary_requests_total` | Counter | status |
| `search_conversation_turns_total` | Counter | intent |
| `search_rag_latency_ms` | Histogram | model, retrieval_k |
| `search_rag_low_confidence_total` | Counter | tenant_id |
| `search_recommendation_latency_ms` | Histogram | context |
| `search_recommendation_ctr` | Gauge | placement |

### Distributed Tracing (OpenTelemetry)

Every request traced end-to-end:

```
[API] → [QueryProcessor] → [CacheCheck] → [Typesense] → [RankingEngine] → [Response]
```

Trace IDs propagated to analytics events for correlation.

### Alerts

| Alert | Condition | Severity |
|---|---|---|
| High latency | P95 > 150ms for 5 minutes | Warning |
| Very high latency | P95 > 300ms for 2 minutes | Critical |
| High error rate | HTTP 5xx > 1% for 5 minutes | Critical |
| Zero-result spike | Zero-result rate > 15% for 10 minutes | Warning |
| Cache miss rate | Cache hit ratio < 20% for 15 minutes | Warning |
| Index staleness | Stock sync lag > 10 minutes | Warning |
| LLM latency spike | P95 > 250ms for 5 minutes | Warning |
| AI summary failure rate | > 2% for 5 minutes | Critical |
| Hybrid fallback surge | Fallback ratio > 15% for 10 minutes | Warning |
| RAG low-confidence spike | > 10% low-confidence answers for 10 minutes | Warning |
| Recommendation CTR drop | CTR decreases > 20% WoW | Warning |

### Dashboards

- **Search Operations:** latency percentiles, QPS, error rate, cache hit ratio
- **Search Quality:** CTR, conversion, zero-result rate, top queries
- **Infrastructure:** Typesense node health, Valkey memory, K8s pod count
- **Phase 2 CronJobs:** last run time, duration, success/failure for each of the 4 nightly jobs
- **Feature Flags:** flag enable/disable events, canary traffic percentage, rollback incidents

---

## 18. Feature Flags & Canary Rollout

### Feature Flags

All Phase 2 feature flags are managed via a Kubernetes ConfigMap in `nexora-prod`:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: nexora-feature-flags
  namespace: nexora-prod
data:
  FEATURE_BEHAVIORAL_RANKING: "true"      # CTR + conversion signals in FinalScore
  FEATURE_PERSONALIZATION: "true"         # Rule-based Valkey affinity profiles
  FEATURE_AB_TESTING: "true"              # A/B test framework active
  FEATURE_ZERO_RESULT_FALLBACK: "true"    # Fuzzy fallback on zero results
```

All flags default to `false`. The Search API reads flags at startup and caches them; a rolling restart is required for flag changes to take effect.

### Phase 2 Canary Rollout Sequence

| Stage | Traffic % | Duration | Gate |
|---|---|---|---|
| Canary | 10% | 48 hours | No CTR regression > 5% |
| Early Majority | 25% | 48 hours | No CTR regression > 5%; P95 latency < 100ms |
| Majority | 50% | 48 hours | No CTR regression > 10%; error rate < 0.5% |
| Full Rollout | 100% | — | All gates passed |

**Rollback Trigger**

If CTR regression exceeds **10%** at any stage:
1. Set `FEATURE_BEHAVIORAL_RANKING=false` and `FEATURE_PERSONALIZATION=false` in ConfigMap
2. Rolling restart completes in < 2 minutes (HPA min=2 ensures availability)
3. Incident is logged; rollback event written to `data_erasure_log` alternative: `ranking_incident_log`

**Traffic Splitting**

Canary traffic is split at the Kubernetes ingress layer (Nginx/AWS ALB weighted target groups). The Search API itself is stateless; all state is in Valkey and PostgreSQL.

## Appendix A: Vertical Slice Architecture Structure

```
src/
├── Search.Api/
│   ├── Program.cs                    # Minimal API bootstrapping
│   ├── Features/
│   │   ├── Search/
│   │   │   ├── SearchEndpoint.cs     # Route definition + handler
│   │   │   ├── SearchRequest.cs      # Input model + validation
│   │   │   ├── SearchResponse.cs     # Output model
│   │   │   ├── SearchHandler.cs      # Orchestrates pipeline
│   │   │   └── SearchValidator.cs    # FluentValidation rules
│   │   ├── Suggest/
│   │   │   ├── SuggestEndpoint.cs
│   │   │   ├── SuggestRequest.cs
│   │   │   ├── SuggestResponse.cs
│   │   │   └── SuggestHandler.cs
│   │   ├── Events/
│   │   │   ├── TrackEventEndpoint.cs
│   │   │   └── TrackEventHandler.cs
│   │   └── Admin/
│   │       ├── Synonyms/
│   │       ├── RankingConfig/
│   │       └── ZeroResultQueries/
│   ├── Infrastructure/
│   │   ├── Typesense/
│   │   │   ├── TypesenseSearchClient.cs
│   │   │   └── TypesenseIndexManager.cs
│   │   ├── Cache/
│   │   │   └── ValkeyCache.cs
│   │   ├── Analytics/
│   │   │   └── EventPublisher.cs
│   │   ├── Personalization/
│   │   │   ├── PersonalizationService.cs     # Valkey affinity profile read/write
│   │   │   └── UserProfileClient.cs
│   │   └── Gdpr/
│   │       └── DataErasureService.cs         # Full erasure + audit log
│   └── Pipeline/
│       ├── QueryNormalizer.cs
│       ├── SpellCorrector.cs
│       ├── SynonymExpander.cs
│       ├── IntentClassifier.cs
│       └── RankingEngine.cs
├── Search.Tests/
│   ├── Unit/
│   ├── Integration/
│   │   ├── AnalyticsPipelineTests.cs
│   │   ├── PersonalizationServiceTests.cs
│   │   ├── DataRetentionTests.cs
│   │   ├── AbTestingFrameworkTests.cs
│   │   └── GdprErasureTests.cs
│   └── Performance/
│       └── event-pipeline.k6.js             # k6 load test ≥ 1000 events/sec
└── Search.Admin/                    # Admin dashboard (separate service)
```

---

## Appendix B: Ranking Weight Changelog

| Date | Change | Rationale | Owner |
|---|---|---|---|
| 2026-05-07 | Initial defaults set | Baseline — no behavioral data | Engineering |

*All weight changes must be logged in this table with rationale and owner.*

---

*Document Owner: Engineering Lead*  
*Last Updated: 2026-05-07*  
*Next Review: Phase 3 technical readiness review*
