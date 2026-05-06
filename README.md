# Nexora — Enterprise Search & Discovery System

**An intelligent, scalable, and adaptive search system for e-commerce marketplaces.**

Nexora delivers sub-100ms query latency, semantic understanding, typo tolerance, and real-time ranking optimization—built on **Typesense**, **.NET 10**, **Valkey**, **RabbitMQ**, **MSSQL Server**, and deployed on **AWS EKS**.

---

## 🎯 Business Objectives

| Target | Measurement |
|--------|-------------|
| **+20%** Search → Add-to-Cart conversion | Analytics dashboard |
| **+15%** Search → Purchase conversion | Transactional data |
| **−30%** Reduce zero-result queries | Query analytics |
| **+25%** Improve Click-Through Rate (CTR) | Event tracking |
| **<100ms** P95 search latency | APM monitoring |

---

## 🏗️ Architecture at a Glance

```
MSSQL Server (Product Source)
    ↓ CDC + Batch Sync
Index Sync Service (.NET 10 Worker)
    ↓ Typesense API
Search API (.NET 10 Minimal API)
    ├── Query Processor (spell correction, synonyms, intent)
    ├── Ranking Engine (FinalScore = 0.40×TextScore + 0.20×CTR + 0.15×Conversion + ...)
    └── Personalization Layer (Phase 2+)
         ↓
    Typesense Cluster (sub-10ms queries, typo tolerance, faceting)
         ↓
    Valkey Cache (sub-ms reads, frequent queries)
         ↓
    Analytics Pipeline (RabbitMQ → DWH)
```

**Full architecture details:** See [ARCHITECTURE.md](./ARCHITECTURE.md)

---

## 🚀 Quick Start (Local Development)

### Prerequisites

- Docker & Docker Compose
- .NET 10 SDK
- PowerShell / Bash

### Start Local Stack

```bash
# Clone the repo
git clone https://github.com/emonarafat/Nexora.git
cd Nexora

# Start dependencies (Typesense, Valkey, RabbitMQ, PostgreSQL, MSSQL)
docker-compose -f docker-compose.local.yml up -d

# Wait for services to be ready (~30s)
docker-compose -f docker-compose.local.yml logs -f

# In a new terminal: Build and run the Search API
dotnet build src/Nexora.SearchAPI/Nexora.SearchAPI.csproj
dotnet run --project src/Nexora.SearchAPI/Nexora.SearchAPI.csproj

# Test the API
curl "http://localhost:5000/api/search?q=running+shoes"

# Admin Dashboard (local)
open http://localhost:3000/admin  # or navigate in your browser
```

---

## 📁 Project Structure

```
Nexora/
├── docs/
│   ├── BRD.md                    # Business Requirements Document
│   ├── TRD.md                    # Technical Requirements Document
│   └── ARCHITECTURE.md           # Detailed system design
├── src/
│   ├── Nexora.SearchAPI/         # Main search API (.NET 10 Minimal API)
│   │   ├── Features/             # Vertical slice: Query, Suggest, Analytics
│   │   ├── Infrastructure/       # Typesense client, Valkey, RabbitMQ integration
│   │   └── Program.cs
│   ├── Nexora.IndexSync/         # Worker service: MSSQL CDC + Typesense upsert
│   ├── Nexora.AdminAPI/          # Admin API for ranking tuning, synonyms (internal)
│   └── Nexora.Shared/            # Shared DTOs, constants, utilities
├── tests/
│   ├── Nexora.SearchAPI.Tests/   # Unit + integration tests
│   └── Nexora.IndexSync.Tests/   # CDC sync tests
├── infra/
│   ├── terraform/                # AWS EKS, RDS, ElastiCache, etc.
│   ├── docker-compose.local.yml  # Local dev environment
│   └── k8s/                      # Kubernetes manifests
├── docker/
│   ├── Dockerfile.SearchAPI
│   ├── Dockerfile.IndexSync
│   └── Dockerfile.AdminAPI
└── .github/
    └── workflows/
        ├── build.yml             # CI: build, test, lint
        ├── deploy-dev.yml        # Deploy to dev EKS
        └── deploy-prod.yml       # Deploy to prod EKS
```

---

## 🔧 Technology Stack

| Layer | Technology | Why |
|-------|-----------|-----|
| **API** | .NET 10 Minimal API | Low latency, native AOT, Vertical Slice architecture |
| **Search Engine** | Typesense | Sub-10ms queries, typo tolerance, low ops |
| **Cache** | Valkey 8 | Redis-compatible, sub-millisecond reads |
| **Message Bus** | RabbitMQ | Durable event queues, topic exchanges |
| **Product Source** | MSSQL Server | Source of truth for product catalog |
| **Metadata DB** | PostgreSQL | Synonyms, ranking overrides, A/B config |
| **Infrastructure** | AWS EKS | Horizontal scaling, zero-downtime deploys |
| **Observability** | OpenTelemetry + Grafana | Vendor-neutral tracing & metrics |

---

## 📈 Key Features

### Phase 1 (MVP)
- ✅ Full-text search with typo tolerance
- ✅ Filtering & faceting (category, price, brand, etc.)
- ✅ Query spell correction
- ✅ Synonym handling
- ✅ Inventory-aware ranking (demote out-of-stock)
- ✅ Analytics event pipeline

### Phase 2
- 🔄 Advanced ranking with behavioral signals (CTR, conversion rate)
- 🔄 A/B testing framework for ranking experiments
- 🔄 Rule-based personalization layer
- 🔄 Admin dashboard for manual overrides

### Phase 3
- 📅 ML-powered personalization
- 📅 Recommendation engine integration
- 📅 Voice search support

---

## 📊 Ranking Formula

```
FinalScore = 
  0.40 × TextScore          (BM25 relevance)
+ 0.20 × CTR               (Click-through rate signals)
+ 0.15 × ConversionRate    (Purchase signals)
+ 0.10 × Availability      (Stock level boost)
+ 0.10 × Rating            (Customer ratings)
+ 0.05 × Personalization   (User affinity, Phase 2+)

Adjustments:
  × 1.5 if featured product
  × 0.2 if out-of-stock
```

---

## 🔒 Security Requirements

- **API Authentication:** JWT + API keys (internal) 
- **Rate Limiting:** Sliding window (1000 req/min per API key)
- **HTTPS/TLS:** Enforced for all external endpoints
- **MSSQL Encryption:** At-rest (TDE) + in-transit (TLS)
- **Secrets Management:** AWS Secrets Manager
- **IAM:** Least privilege roles for EKS pods

---

## 📞 API Examples

### Search

```bash
curl "http://localhost:5000/api/search" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "query": "running shoes",
    "filters": {
      "category": "footwear",
      "price_range": { "min": 50, "max": 200 }
    },
    "page": 1,
    "size": 20
  }'
```

**Response:**
```json
{
  "results": [
    {
      "id": "prod_12345",
      "title": "Nike Air Running Shoes",
      "price": 120,
      "rating": 4.5,
      "in_stock": true,
      "final_score": 98.5
    }
  ],
  "total": 1240,
  "facets": {
    "category": [
      { "value": "athletic_shoes", "count": 520 },
      { "value": "casual_shoes", "count": 310 }
    ]
  }
}
```

### Typeahead

```bash
curl "http://localhost:5000/api/suggest?q=run&limit=8"
```

---

## 🧪 Testing Strategy

- **Unit Tests:** XUnit / TUnit, ≥80% code coverage
- **Integration Tests:** Testcontainers for Typesense, PostgreSQL, Valkey
- **Load Tests:** k6 / Locust (target: sub-100ms P95 at 10k req/s)
- **E2E Tests:** Playwright (client scenarios via test admin account)

Run tests:
```bash
dotnet test
```

---

## 📈 Monitoring & Observability

- **Metrics:** OpenTelemetry → Prometheus → Grafana
- **Traces:** Request flow, latency breakdown per component
- **Logs:** Structured logging (Serilog), shipped to CloudWatch
- **Dashboards:** Query latency, error rates, cache hit ratio, sync lag

---

## 🤝 Contributing

We welcome contributions! See [CONTRIBUTING.md](./CONTRIBUTING.md) for guidelines.

---

## 📜 License

MIT License — See [LICENSE](./LICENSE) for details.

---

## 📧 Support

- **Documentation:** [docs/](./docs/)
- **Issues:** [GitHub Issues](https://github.com/emonarafat/Nexora/issues)
- **Discussions:** [GitHub Discussions](https://github.com/emonarafat/Nexora/discussions)

---

**Built with ❤️ by the Nexora team**
