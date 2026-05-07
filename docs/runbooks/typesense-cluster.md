# Typesense Cluster Runbook (EKS)

## Scope

Applies to `dev`, `staging`, and `prod` Typesense clusters provisioned from:

- `infra/terraform/typesense`

Cluster baseline:

- 3 nodes (`1 leader + 2 replicas`)
- `4 vCPU / 8Gi` per node
- Stateful EBS-backed storage
- Private in-cluster access (`ClusterIP` + network policy)

---

## 1) Provision / Update

```bash
cd infra/terraform/typesense
terraform init
terraform apply -var-file=environments/<env>.tfvars
```

Post-deploy checks:

```bash
kubectl get pods -n nexora-<env> -l app=typesense
kubectl get svc -n nexora-<env> typesense
kubectl get pvc -n nexora-<env>
```

Healthy target:

- 3/3 pods `Running`
- all PVCs `Bound`
- replication lag metric `< 1s`

---

## 2) API Key Rotation (No Downtime)

1. Update the secret value in AWS Secrets Manager (`/nexora/<env>/typesense/api-key`).
2. Restart one Typesense pod at a time to pick up the new key:
   ```bash
   kubectl rollout restart statefulset/typesense -n nexora-<env>
   kubectl rollout status statefulset/typesense -n nexora-<env>
   ```
3. Validate read/write queries from Search API before rotating next environment.

If `typesense_api_key_rotation_lambda_arn` is configured, Secrets Manager rotates every 30 days automatically.

---

## 3) Backup & Restore

### Hourly backups

- Backups are stored in `nexora-typesense-<env>-backups` S3 bucket.
- Verify recent objects:
  ```bash
  aws s3 ls s3://nexora-typesense-<env>-backups/ --recursive | tail
  ```

### Weekly restore test (required)

1. Restore latest snapshot into an isolated `nexora-restore-test` namespace.
2. Run smoke query against restored cluster (`/health` and one known document lookup).
3. Capture evidence in operations log: snapshot ID, restore timestamp, success/failure.
4. Remove restore-test resources.

Failing restore test is a sev-2 operational incident.

---

## 4) Node Replacement

1. Identify unhealthy pod (`kubectl get pods -o wide`).
2. Cordon and drain affected node.
3. Delete failing Typesense pod; StatefulSet recreates it with existing PVC:
   ```bash
   kubectl delete pod <typesense-pod> -n nexora-<env>
   ```
4. Verify cluster rejoins and replication lag returns below 1s.

---

## 5) Cluster Upgrade

1. Change `typesense_image` in `<env>.tfvars` (one environment at a time).
2. Apply Terraform.
3. Validate:
   - `/health` for all nodes
   - query latency dashboard
   - replication lag alarm remains clear

Promote in order: `dev -> staging -> prod`.

---

## 6) Emergency Rollback

1. Revert `typesense_image` to last known-good version.
2. `terraform apply -var-file=environments/<env>.tfvars`
3. If data corruption suspected, restore from latest validated snapshot.
4. Keep rollback evidence: alarm timeline, commands used, validation output.

---

## 7) Monitoring & Alerts

CloudWatch dashboard and alarms are provisioned per environment:

- `QueryLatencyMs` (P95)
- `ReplicationLagSeconds` (max)
- `ServerErrors` (sum)

On-call thresholds:

- latency P95 > 100ms for 3 minutes
- replication lag > 1s for 3 minutes
- any server errors sustained for 2 minutes
