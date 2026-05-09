# Security Control Matrix - Phase 1

## Endpoint Authentication Requirements

| Endpoint | Auth Method | Rate Limit | Input Validation | Notes |
|----------|-------------|------------|------------------|-------|
| **Public Search API** |
| `GET /api/v1/search` | API Key (`X-API-Key` header) | 60 req/min per API key | Query sanitization, filter validation | Primary search endpoint |
| `GET /api/v1/suggest` | API Key (`X-API-Key` header) | 120 req/min per API key | Min 2 chars, max length | Typeahead suggestions |
| `GET /health` | None (public) | None | None | Health check for load balancers |
| `GET /metrics` | Internal only | None | None | Prometheus scraping endpoint |
| **Admin API** |
| `GET /api/v1/admin/synonyms` | JWT Bearer Token | 30 req/min per user | - | List synonyms |
| `POST /api/v1/admin/synonyms` | JWT Bearer Token | 30 req/min per user | Synonym validation | Create synonym |
| `DELETE /api/v1/admin/synonyms/{term}` | JWT Bearer Token | 30 req/min per user | - | Delete synonym |
| `POST /api/v1/admin/reindex` | JWT Bearer Token | 10 req/min per user | - | Trigger reindex operation |
| `POST /api/v1/suggest/cache/invalidate` | JWT Bearer Token | 30 req/min per user | - | Invalidate suggest cache |
| `GET /health` | None (public) | None | None | Health check |
| **Analytics Event API** (Future Phase 2) |
| `POST /api/v1/events` | Service Identity + mTLS | 1000 events/min per service | Event schema validation | Analytics event ingestion |

## Authentication Implementation Status

### ✅ Implemented
- JWT Bearer authentication for internal APIs (SearchAPI, AdminAPI)
- Token validation with issuer, audience, lifetime checks
- Authorization middleware registered
- Development/Test mode relaxed validation

### ⚠️ Phase 1.10 Additions Required
- **API Key authentication middleware** for public Search API endpoints
- API key validation against configured key store
- Per-key rate limiting tracking
- API key rotation mechanism

## Rate Limiting Implementation Status

### ✅ Implemented
- Search endpoint: 1000 req/min sliding window (currently not per-key)
- Rate limit headers: `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`
- Deep pagination blocking (page > 50 returns 429)

### ⚠️ Phase 1.10 Additions Required
- **Suggest endpoint rate limiting**: 120 req/min per API key
- **Admin endpoint rate limiting**: 30 req/min per user
- **Per-API-key tracking** for Search and Suggest endpoints
- ReIndex endpoint: 10 req/min per user (stricter limit)

## Input Validation Implementation Status

### ✅ Implemented
- Query sanitization (SQL/NoSQL injection pattern detection)
- HTML/script tag removal
- Control character stripping
- Query length limits (200 chars)
- Filter expression validation with field whitelisting
- `merchant_id` blocking in client filters
- Suspicious pattern detection in filters
- Deep pagination blocking

### ✅ Test Coverage
- QuerySanitizerTests (SQL injection, XSS, length)
- SearchRequestValidatorTests (injection, field restrictions, merchant_id blocking)
- Filter validation tests

### ⚠️ Phase 1.10 Additions Required
- Additional negative test cases for filter bypass attempts
- Unicode normalization attack tests
- LDAP injection pattern tests
- Path traversal tests (if applicable to future file operations)

## Secrets Management

### Current State
- Secrets stored in `appsettings.json` with placeholder values
- Environment variable override support
- `.gitignore` excludes secrets files
- Kubernetes `secretKeyRef` integration in deployment manifests

### ⚠️ Phase 1.10 Additions Required
- **API Key Rotation Runbook**: Step-by-step procedure
- **JWT Key Rotation Runbook**: Zero-downtime rotation process
- **Typesense API Key Rotation**: Quarterly rotation schedule
- **Database Credential Rotation**: Integration with secrets manager
- Key versioning and rollback procedure

## CI/CD Security

### Current State
- Build and test pipeline in GitHub Actions
- Minimal permissions model (`contents: read`)
- Docker image builds for main branch

### ⚠️ Phase 1.10 Additions Required
- **Trivy container image scanning**: Scan for vulnerabilities in Docker images
- **Dependency vulnerability scanning**: Check for vulnerable NuGet packages
- **OWASP Top 10 compliance checks**
- Security scan failure blocks merge/release
- Vulnerability report artifacts

## Security Monitoring & Alerting

### Current State
- OpenTelemetry tracing with correlation IDs
- Request/response logging with structured context
- Prometheus metrics export at `/metrics`
- Basic health checks

### ⚠️ Phase 1.10 Additions Required
- **Auth failure metrics**: Counter for failed API key validations
- **Rate limit abuse metrics**: Track repeated rate limit violations per key
- **Injection attempt metrics**: Counter for blocked malicious queries
- **Security dashboard**: Grafana dashboard with security panels
- **Alerting rules**: PagerDuty/Slack alerts for security events
- Audit log for admin operations

## Compliance Checklist for Phase 1 Release

This checklist must be completed and signed off before Phase 1 release:

### Authentication ✅
- [ ] All public endpoints require API key authentication
- [ ] API keys are validated on every request
- [ ] JWT tokens validated for admin operations
- [ ] No endpoints exposed without intended auth mode

### Rate Limiting ✅
- [ ] Search endpoint enforces 60 req/min per API key
- [ ] Suggest endpoint enforces 120 req/min per API key
- [ ] Admin endpoints enforce 30 req/min per user
- [ ] Rate limit headers returned on all responses
- [ ] Rate limiting behavior tested under load

### Input Validation ✅
- [ ] Query sanitization active and tested
- [ ] Filter expression validation prevents merchant_id injection
- [ ] All injection negative tests pass
- [ ] Field whitelisting enforced
- [ ] Deep pagination blocked

### Secrets Management ✅
- [ ] No secrets committed to source control
- [ ] API key rotation runbook documented
- [ ] JWT key rotation tested without downtime
- [ ] Kubernetes secrets configured for production
- [ ] Secrets rotation schedule established

### Vulnerability Management ✅
- [ ] CI pipeline includes Trivy container scanning
- [ ] No critical vulnerabilities in container images
- [ ] No high/critical vulnerabilities in dependencies
- [ ] Dependency update process automated (Renovate/Dependabot)
- [ ] Security scan failures block merge

### Monitoring & Alerting ✅
- [ ] Auth failure metrics exported
- [ ] Rate limit abuse alerts configured
- [ ] Security dashboard deployed
- [ ] Incident response runbook documented
- [ ] Alert routing to on-call team configured

### Documentation ✅
- [ ] Security control matrix reviewed and approved
- [ ] API key rotation runbook tested
- [ ] Incident response procedures documented
- [ ] Security architecture diagram updated
- [ ] Penetration test findings remediated (if applicable)

## Sign-off

**Security Lead:** ___________________ Date: ___________

**Engineering Lead:** ___________________ Date: ___________

**Product Owner:** ___________________ Date: ___________

---

## References
- [TRD.md Section 14: Security Requirements](./TRD.md#14-security-requirements)
- [ARCHITECTURE.md Section 13: Security & Data Protection](../ARCHITECTURE.md)
- [API Key Rotation Runbook](./runbooks/API_KEY_ROTATION.md)
- [Incident Response Runbook](./runbooks/SECURITY_INCIDENT_RESPONSE.md)
