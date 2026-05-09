# Security Incident Response Runbook

## Overview
This runbook provides step-by-step procedures for responding to security incidents in the Nexora Search Platform.

## Incident Classification

### Severity Levels

| Level | Description | Response Time | Examples |
|-------|-------------|---------------|----------|
| **P0 - Critical** | Active exploitation, data breach, complete system compromise | Immediate (< 15 min) | Production database breach, active RCE exploit, credential leak in public repo |
| **P1 - High** | Potential for exploitation, security control failure | < 1 hour | Failed authentication spike, rate limit bypass discovered, critical CVE in dependency |
| **P2 - Medium** | Security weakness identified, no active exploitation | < 4 hours | XSS vulnerability found in testing, deprecated TLS version detected |
| **P3 - Low** | Minor security concern, low risk | < 24 hours | Outdated documentation, informational security scan findings |

## Incident Response Phases

### Phase 1: Detection & Triage (0-15 minutes)

**Alerting Channels:**
- PagerDuty notifications
- Security monitoring dashboard alerts
- AWS GuardDuty findings
- GitHub Advanced Security alerts
- Manual reports from users/staff

**Initial Actions:**

1. **Acknowledge the incident**
   ```bash
   # Log incident in tracking system
   incident-cli create --severity=P1 \
     --title="API Key Exposure Detected" \
     --description="Potential API key leak in public GitHub repository"
   ```

2. **Assemble incident response team**
   - Incident Commander (IC)
   - Security Engineer
   - Platform Engineer
   - Communications Lead (for P0/P1)

3. **Initial assessment**
   - What systems are affected?
   - Is exploitation active or potential?
   - What data/services are at risk?
   - Assign severity level

### Phase 2: Containment (15 min - 2 hours)

**Immediate Containment Actions:**

#### For API Key/Credential Compromise

1. **Revoke compromised credentials immediately**
   ```bash
   # Revoke API key
   kubectl delete secret api-key-production -n nexora-prod

   # Deploy emergency key rotation
   kubectl apply -f emergency-api-keys.yaml

   # Force restart all pods to pickup new keys
   kubectl rollout restart deployment/search-api -n nexora-prod
   ```

2. **Block malicious IP addresses**
   ```bash
   # Add to AWS WAF block list
   aws wafv2 update-ip-set \
     --name nexora-blocked-ips \
     --scope REGIONAL \
     --id <ip-set-id> \
     --addresses <malicious-ip>/32
   ```

3. **Enable enhanced monitoring**
   ```bash
   # Increase log verbosity temporarily
   kubectl set env deployment/search-api LOG_LEVEL=DEBUG -n nexora-prod

   # Enable audit logging
   kubectl label namespace nexora-prod audit=enabled
   ```

#### For Injection/Exploitation Attempt

1. **Deploy emergency rate limiting**
   ```yaml
   # emergency-rate-limit.yaml
   apiVersion: v1
   kind: ConfigMap
   metadata:
     name: emergency-rate-limits
   data:
     SEARCH_RATE_LIMIT: "10"  # Reduced from 60
     SUGGEST_RATE_LIMIT: "20"  # Reduced from 120
   ```

2. **Enable WAF strict mode**
   ```bash
   aws wafv2 update-web-acl \
     --name nexora-waf \
     --scope REGIONAL \
     --id <acl-id> \
     --default-action Block={}
   ```

3. **Isolate affected services**
   ```bash
   # Scale down affected deployment
   kubectl scale deployment/search-api --replicas=0 -n nexora-prod

   # Deploy patched version to isolated namespace
   kubectl apply -f patched-deployment.yaml -n nexora-isolated
   ```

#### For Data Breach

1. **Snapshot affected databases**
   ```bash
   # Take immediate PostgreSQL snapshot
   aws rds create-db-snapshot \
     --db-instance-identifier nexora-prod \
     --db-snapshot-identifier incident-$(date +%Y%m%d-%H%M%S)
   ```

2. **Revoke database access**
   ```sql
   -- Revoke all non-essential database access
   REVOKE ALL PRIVILEGES ON DATABASE nexora_metadata FROM nexora_user;

   -- Create emergency read-only user for investigation
   CREATE USER incident_investigator WITH PASSWORD '<strong-password>';
   GRANT SELECT ON ALL TABLES IN SCHEMA public TO incident_investigator;
   ```

3. **Isolate network segments**
   ```bash
   # Update security group to restrict access
   aws ec2 revoke-security-group-ingress \
     --group-id <db-security-group> \
     --protocol tcp \
     --port 5432 \
     --cidr 0.0.0.0/0
   ```

### Phase 3: Eradication (2-8 hours)

**Root Cause Analysis:**

1. **Collect forensic evidence**
   ```bash
   # Export logs for analysis
   kubectl logs deployment/search-api -n nexora-prod \
     --since=24h > incident-logs.txt

   # Export metrics
   promtool query range \
     'rate(http_requests_total{status="401"}[5m])' \
     --start=$(date -d '24 hours ago' --iso-8601=seconds) \
     --end=$(date --iso-8601=seconds) \
     > auth-failure-metrics.json
   ```

2. **Analyze attack vectors**
   - Review access logs for suspicious patterns
   - Check for indicators of compromise (IOCs)
   - Identify vulnerability exploited

3. **Develop permanent fix**
   - Patch vulnerable code
   - Update dependencies
   - Strengthen security controls

**Apply Permanent Fixes:**

```bash
# Deploy patched version
git checkout -b hotfix/security-incident-$(date +%Y%m%d)
# ... make fixes ...
git commit -m "Security fix: [INCIDENT-123] Patch SQL injection vulnerability"
git push origin hotfix/security-incident-$(date +%Y%m%d)

# Emergency deployment to production
kubectl set image deployment/search-api \
  search-api=nexora/searchapi:patched-$(git rev-parse --short HEAD) \
  -n nexora-prod

# Monitor rollout
kubectl rollout status deployment/search-api -n nexora-prod
```

### Phase 4: Recovery (4-24 hours)

**Service Restoration:**

1. **Gradually restore normal operations**
   ```bash
   # Restore normal rate limits
   kubectl delete configmap emergency-rate-limits -n nexora-prod

   # Scale back to normal replica count
   kubectl scale deployment/search-api --replicas=3 -n nexora-prod

   # Re-enable normal WAF rules
   aws wafv2 update-web-acl \
     --name nexora-waf \
     --default-action Allow={}
   ```

2. **Verify system integrity**
   ```bash
   # Run security scan
   trivy image nexora/searchapi:latest

   # Verify no persistence mechanisms
   kubectl exec -it deployment/search-api -- find / -name "*.sh" -mtime -1
   ```

3. **Restore monitoring to normal levels**
   ```bash
   kubectl set env deployment/search-api LOG_LEVEL=INFO -n nexora-prod
   ```

**Data Integrity Checks:**

```sql
-- Verify no unauthorized data modifications
SELECT table_name, pg_size_pretty(pg_total_relation_size(table_name::regclass))
FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY pg_total_relation_size(table_name::regclass) DESC;

-- Check for suspicious recent changes
SELECT * FROM data_audit_log
WHERE event_at > NOW() - INTERVAL '24 hours'
AND event_type IN ('DELETE', 'TRUNCATE');
```

### Phase 5: Post-Incident (24-72 hours)

**Post-Mortem Report:**

Template: `docs/incident-reports/YYYY-MM-DD-incident-summary.md`

```markdown
# Incident Report: [INCIDENT-ID]

## Executive Summary
- **Incident Date:** YYYY-MM-DD HH:MM UTC
- **Severity:** P1 - High
- **Duration:** X hours Y minutes
- **Impact:** Brief description

## Timeline
| Time (UTC) | Event |
|------------|-------|
| 14:23 | Alert triggered: High rate of 401 responses |
| 14:30 | IC paged, response team assembled |
| 14:45 | Containment: Revoked compromised API key |
| 16:00 | Eradication: Deployed patched version |
| 18:30 | Recovery: Full service restored |

## Root Cause
Detailed analysis of vulnerability exploited...

## Impact Assessment
- **Users affected:** X
- **Data exposed:** None / [specific data categories]
- **Services impacted:** SearchAPI (full outage 2h)
- **Financial impact:** Estimated $X in lost revenue

## Remediation Actions Taken
1. Immediate: Revoked compromised credentials
2. Short-term: Deployed hotfix patch
3. Long-term: Enhanced input validation, added WAF rules

## Lessons Learned
### What Went Well
- Fast detection via monitoring alerts
- Effective communication within team
- Clean rollback procedures worked as expected

### What Could Be Improved
- Delayed paging due to misconfigured PagerDuty
- Insufficient documentation for emergency procedures
- No automated credential rotation in place

## Action Items
- [ ] Implement automated API key rotation (Owner: Security, Due: 2026-06-01)
- [ ] Add integration tests for injection patterns (Owner: QA, Due: 2026-05-20)
- [ ] Update incident response runbook (Owner: DevOps, Due: 2026-05-15)
- [ ] Conduct tabletop exercise (Owner: Security, Due: 2026-06-15)
```

**Security Improvements:**

1. **Update threat model**
2. **Add detection rules for similar attacks**
3. **Conduct security training for team**
4. **Schedule penetration test if needed**

## Communication Templates

### Internal Notification (P0/P1)

**Subject:** [P1 INCIDENT] Security Event - Nexora Search Platform

```
Team,

We are currently investigating a P1 security incident affecting the Nexora Search Platform.

Status: INVESTIGATING
Incident ID: INC-2026-0509-001
Started: 2026-05-09 14:23 UTC
Severity: P1 - High
Impact: Search API experiencing elevated 401 error rates

Current Actions:
- Incident response team assembled
- Containment procedures in progress
- Monitoring for additional indicators

Next Update: 30 minutes or when status changes

Incident Commander: alice@nexora.com
War Room: https://zoom.us/j/incident-room
Status Page: https://status.nexora.com

Do not share this information externally.
```

### Customer Notification (if required)

**Subject:** Nexora Security Update

```
Dear Nexora Customer,

We are writing to inform you of a security incident that occurred on May 9, 2026.

What Happened:
On May 9 at approximately 14:23 UTC, we detected unauthorized access attempts to our Search API using a compromised API key.

What We Did:
We immediately revoked the compromised key, rotated all API keys, and deployed additional security measures. The incident was fully contained by 16:00 UTC.

What Data Was Affected:
No customer data was accessed or exposed during this incident.

What We're Doing:
We have implemented enhanced monitoring, updated our security controls, and are conducting a thorough security review.

What You Should Do:
- New API keys have been issued and are available in your dashboard
- Review your recent API usage for any anomalies
- Update your applications with the new keys by May 15, 2026

If you have questions, please contact security@nexora.com.

Sincerely,
Nexora Security Team
```

## Contacts

- **Security Team:** security@nexora.com
- **On-Call Engineer:** PagerDuty escalation
- **Incident Commander (Primary):** alice@nexora.com
- **Incident Commander (Backup):** bob@nexora.com
- **Legal/Compliance:** compliance@nexora.com

## Tools & Resources

- **Incident Tracking:** https://jira.nexora.com/incidents
- **Monitoring Dashboard:** https://grafana.nexora.com/security
- **Log Analysis:** https://kibana.nexora.com
- **Status Page:** https://status.nexora.com
- **War Room:** https://zoom.us/j/incident-room

---

**Last Updated:** 2026-05-09
**Document Owner:** Security Team
**Review Frequency:** Quarterly or after each P0/P1 incident
