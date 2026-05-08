# Index Sync Runbook

## Symptoms
- `search_index_sync_lag_seconds{source="product"}` above 60
- `search_index_sync_lag_seconds{source="stock_price"}` above 300
- `search_index_sync_errors_total` increasing
- Full re-index not starting on schedule or after manual trigger

## Checks
1. Verify the worker is healthy: `GET /health`
2. Verify Prometheus metrics: `GET /metrics`
3. Verify MSSQL CDC is enabled for `products`, `stock`, and `pricing`
4. Check the `dbo.sync_dead_letter` table for poison records
5. Review worker logs for retry exhaustion or Typesense import failures

## Manual Re-index
Trigger via Admin API:

```bash
curl -X POST http://<admin-api>/api/v1/admin/reindex
```

The Admin API forwards the request to the IndexSync internal endpoint.

## Dead-letter Recovery
1. Inspect failed rows in `dbo.sync_dead_letter`
2. Fix bad source data or downstream availability issues
3. Re-run a full re-index after the issue is resolved

## Common Failure Modes
- **MSSQL unavailable**: CDC polling logs an error and retries next poll cycle
- **Typesense unavailable**: batch retries use exponential backoff up to 5 attempts, then poison records are written to `dbo.sync_dead_letter`
- **Manual re-index not working**: confirm `IndexSync:BaseUrl` is configured in AdminAPI settings
