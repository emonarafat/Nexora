# API Key Rotation Runbook

## Overview
This runbook describes the zero-downtime procedure for rotating API keys used by Nexora Search Platform.

## Key Types and Rotation Schedule

| Key Type | Rotation Frequency | Impact | Priority |
|----------|-------------------|--------|----------|
| Public Search API Keys | Quarterly (90 days) | External clients | High |
| Typesense Admin API Key | Quarterly (90 days) | Internal services | Critical |
| JWT Signing Key | Annually (365 days) | Admin users | Medium |
| Database Credentials | Annually (365 days) | Application layer | High |
| Valkey/Redis Password | Annually (365 days) | Cache layer | Medium |
| RabbitMQ Credentials | Annually (365 days) | Event pipeline | Medium |

## Prerequisites

- [ ] Access to AWS Secrets Manager (or equivalent secrets management system)
- [ ] Access to Kubernetes cluster with appropriate RBAC permissions
- [ ] Communication plan for notifying API consumers
- [ ] Rollback plan validated

## Procedure 1: Public Search API Key Rotation

**Timeline:** 30-day grace period for client migration

### Phase 1: Generate New Keys (Day 0)

1. Generate new API keys with sufficient entropy:
   ```bash
   # Generate secure random API key (32 bytes, base64 encoded)
   openssl rand -base64 32
   ```

2. Store new keys in secrets manager:
   ```bash
   # AWS Secrets Manager example
   aws secretsmanager create-secret \
     --name nexora/prod/api-keys-v2 \
     --description "Nexora public API keys - Generation 2" \
     --secret-string '{
       "client_a": "NEW_API_KEY_CLIENT_A",
       "client_b": "NEW_API_KEY_CLIENT_B"
     }'
   ```

3. Update application configuration to accept BOTH old and new keys (dual-acceptance mode):
   ```json
   {
     "ApiKeys": {
       "ValidKeys": [
         "OLD_KEY_CLIENT_A",
         "OLD_KEY_CLIENT_B",
         "NEW_KEY_CLIENT_A",
         "NEW_KEY_CLIENT_B"
       ]
     }
   }
   ```

4. Deploy updated configuration:
   ```bash
   kubectl set env deployment/search-api \
     -n nexora-prod \
     API_KEYS_SECRET_VERSION=v2

   kubectl rollout status deployment/search-api -n nexora-prod
   ```

5. Verify both old and new keys are accepted:
   ```bash
   # Test with old key
   curl -H "X-API-Key: OLD_KEY_CLIENT_A" \
     https://api.nexora.com/api/v1/search?q=test

   # Test with new key
   curl -H "X-API-Key: NEW_KEY_CLIENT_A" \
     https://api.nexora.com/api/v1/search?q=test
   ```

### Phase 2: Notify Clients (Day 0-7)

1. Send rotation notice to all API consumers:
   - Email with subject: "ACTION REQUIRED: Nexora API Key Rotation - 30 Day Notice"
   - Include new API keys
   - Provide migration deadline (Day 30)
   - Link to migration guide

2. Update API documentation with new keys (if applicable for test environments)

3. Create monitoring dashboard to track usage of old vs new keys:
   ```promql
   # Prometheus query
   rate(api_requests_total{api_key_version="old"}[5m])
   rate(api_requests_total{api_key_version="new"}[5m])
   ```

### Phase 3: Monitor Migration (Day 7-28)

1. Daily check of old key usage:
   ```bash
   # Query Prometheus for clients still using old keys
   curl 'http://prometheus:9090/api/v1/query' \
     --data-urlencode 'query=sum by (client_id) (rate(api_requests_total{api_key_version="old"}[1h]))'
   ```

2. Send reminder emails to clients with >10% traffic on old keys (Day 14, Day 21)

3. Log all API key validation attempts for audit trail

### Phase 4: Deprecate Old Keys (Day 28)

1. Final warning email: "Old API keys will be revoked in 48 hours"

2. Reduce monitoring interval to hourly

3. Prepare rollback procedure in case of issues

### Phase 5: Revoke Old Keys (Day 30)

1. Remove old keys from valid key list:
   ```json
   {
     "ApiKeys": {
       "ValidKeys": [
         "NEW_KEY_CLIENT_A",
         "NEW_KEY_CLIENT_B"
       ]
     }
   }
   ```

2. Deploy configuration update:
   ```bash
   kubectl set env deployment/search-api \
     -n nexora-prod \
     API_KEYS_SECRET_VERSION=v2-final

   kubectl rollout status deployment/search-api -n nexora-prod
   ```

3. Verify old keys are rejected (expect 401 Unauthorized):
   ```bash
   curl -H "X-API-Key: OLD_KEY_CLIENT_A" \
     https://api.nexora.com/api/v1/search?q=test
   # Expected: {"error": "Invalid API key", "status": 401}
   ```

4. Monitor error rates for spike in 401 responses:
   ```bash
   # Alert if 401 rate > 5% of total requests
   rate(http_requests_total{status_code="401"}[5m]) /
   rate(http_requests_total[5m]) > 0.05
   ```

5. Archive old keys in secrets manager with "REVOKED_" prefix:
   ```bash
   aws secretsmanager update-secret \
     --secret-id nexora/prod/api-keys-v1 \
     --description "REVOKED on 2026-06-09 - Do not use"
   ```

### Rollback Procedure

If critical issues arise after old key revocation:

1. Immediately restore old keys to valid list:
   ```bash
   kubectl set env deployment/search-api \
     -n nexora-prod \
     API_KEYS_SECRET_VERSION=v2 # dual-acceptance mode
   ```

2. Notify affected clients of temporary restoration

3. Investigate root cause and extend migration period

4. Re-attempt revocation after resolution

---

## Procedure 2: Typesense Admin API Key Rotation

**Timeline:** Zero-downtime rotation with scoped key overlap

### Steps

1. Generate new Typesense API key:
   ```bash
   # Connect to Typesense admin console
   curl -X POST "http://typesense:8108/keys" \
     -H "X-TYPESENSE-API-KEY: ${CURRENT_ADMIN_KEY}" \
     -H "Content-Type: application/json" \
     -d '{
       "description": "Nexora Search API - Generation 2",
       "actions": ["documents:search", "collections:get"],
       "collections": ["products"]
     }'
   ```

2. Store new key in secrets manager:
   ```bash
   kubectl create secret generic typesense-api-key-v2 \
     -n nexora-prod \
     --from-literal=api-key=NEW_TYPESENSE_KEY
   ```

3. Update SearchAPI deployment to use new secret:
   ```yaml
   # search-api-deployment.yaml
   env:
     - name: Typesense__ApiKey
       valueFrom:
         secretKeyRef:
           name: typesense-api-key-v2
           key: api-key
   ```

4. Apply deployment:
   ```bash
   kubectl apply -f infra/k8s/search-api-deployment.yaml
   kubectl rollout status deployment/search-api -n nexora-prod
   ```

5. Verify search functionality:
   ```bash
   kubectl logs -n nexora-prod deployment/search-api --tail=50 | grep "Typesense"
   ```

6. Delete old Typesense key:
   ```bash
   # List keys
   curl "http://typesense:8108/keys" \
     -H "X-TYPESENSE-API-KEY: ${NEW_ADMIN_KEY}"

   # Delete old key by ID
   curl -X DELETE "http://typesense:8108/keys/${OLD_KEY_ID}" \
     -H "X-TYPESENSE-API-KEY: ${NEW_ADMIN_KEY}"
   ```

7. Remove old Kubernetes secret:
   ```bash
   kubectl delete secret typesense-api-key-v1 -n nexora-prod
   ```

---

## Procedure 3: JWT Signing Key Rotation

**Timeline:** Zero-downtime with dual-key validation

### Steps

1. Generate new HMAC-SHA256 signing key (minimum 256 bits):
   ```bash
   openssl rand -base64 32
   ```

2. Update configuration to accept both old and new keys:
   ```json
   {
     "Jwt": {
       "Keys": [
         {
           "KeyId": "key-2026-q2",
           "Key": "NEW_JWT_SIGNING_KEY_BASE64",
           "ValidFrom": "2026-06-09T00:00:00Z"
         },
         {
           "KeyId": "key-2025-q2",
           "Key": "OLD_JWT_SIGNING_KEY_BASE64",
           "ValidUntil": "2026-07-09T00:00:00Z"
         }
       ],
       "Issuer": "nexora",
       "Audience": "nexora-api"
     }
   }
   ```

3. Deploy AdminAPI with dual-key validation:
   ```bash
   kubectl set env deployment/admin-api \
     -n nexora-prod \
     JWT_KEY_VERSION=dual-key-2026q2

   kubectl rollout status deployment/admin-api -n nexora-prod
   ```

4. Update token issuance to use new key:
   - New tokens signed with `key-2026-q2`
   - Old tokens (signed with `key-2025-q2`) still validate until expiry

5. Wait for all old tokens to expire (based on max token lifetime, typically 1 hour for admin tokens)

6. After grace period (24 hours recommended), remove old key from configuration

7. Verify only new tokens accepted:
   ```bash
   # Attempt to use old token (expect 401)
   curl -H "Authorization: Bearer OLD_JWT_TOKEN" \
     https://admin.nexora.com/api/v1/admin/synonyms
   ```

---

## Procedure 4: Database Credential Rotation

**Timeline:** Zero-downtime with credential overlap (if RDS/managed DB supports)

### PostgreSQL Rotation (AWS RDS)

1. Create new database user with same permissions:
   ```sql
   CREATE USER nexora_user_v2 WITH PASSWORD 'NEW_SECURE_PASSWORD';
   GRANT ALL PRIVILEGES ON DATABASE nexora_metadata TO nexora_user_v2;
   GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO nexora_user_v2;
   GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO nexora_user_v2;
   ```

2. Update connection string in secrets manager:
   ```bash
   kubectl create secret generic postgres-connection-v2 \
     -n nexora-prod \
     --from-literal=connection-string="Server=postgres.rds.amazonaws.com;Database=nexora_metadata;User Id=nexora_user_v2;Password=NEW_SECURE_PASSWORD;"
   ```

3. Update applications to use new secret:
   ```bash
   kubectl set env deployment/admin-api \
     -n nexora-prod \
     ConnectionStrings__Postgres=$(kubectl get secret postgres-connection-v2 -o jsonpath='{.data.connection-string}' | base64 -d)
   ```

4. Rolling restart of all pods:
   ```bash
   kubectl rollout restart deployment/admin-api -n nexora-prod
   kubectl rollout restart deployment/index-sync -n nexora-prod
   ```

5. Verify connectivity:
   ```bash
   kubectl logs -n nexora-prod deployment/admin-api --tail=20 | grep -i "database"
   ```

6. After verification period (24-48 hours), revoke old user:
   ```sql
   REVOKE ALL PRIVILEGES ON DATABASE nexora_metadata FROM nexora_user_v1;
   DROP USER nexora_user_v1;
   ```

---

## Post-Rotation Checklist

- [ ] All applications using new credentials
- [ ] Old credentials revoked from secrets manager
- [ ] Monitoring dashboards updated with new key versions
- [ ] Audit log entry created documenting rotation
- [ ] Rotation date recorded in key inventory spreadsheet
- [ ] Next rotation reminder set (calendar event)
- [ ] Incident response contacts notified of changes

## Emergency Credential Revocation

If a key compromise is suspected:

1. **Immediate Actions (within 1 hour):**
   - Revoke compromised key from all systems
   - Generate and deploy new key immediately
   - Enable enhanced logging and monitoring
   - Notify security team and stakeholders

2. **Investigation (within 24 hours):**
   - Review access logs for anomalous activity
   - Identify source of compromise
   - Assess impact and data exposure
   - Document timeline of events

3. **Remediation (within 48 hours):**
   - Rotate all related credentials
   - Patch vulnerability if identified
   - Update incident response procedures
   - Conduct post-mortem review

## Contacts

- **Security Team:** security@nexora.com
- **On-Call Engineer:** PagerDuty escalation
- **DevOps Lead:** devops-lead@nexora.com

## Audit Trail

| Rotation Date | Key Type | Performed By | Notes |
|--------------|----------|--------------|-------|
| 2026-03-15 | Public API Keys | alice@nexora.com | Quarterly rotation, all clients migrated successfully |
| 2026-03-20 | Typesense API | bob@nexora.com | Zero downtime, completed in 10 minutes |

---

**Last Updated:** 2026-05-09
**Document Owner:** Security Team
**Review Frequency:** Quarterly
