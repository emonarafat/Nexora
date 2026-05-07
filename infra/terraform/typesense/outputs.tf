output "typesense_namespace" {
  value       = kubernetes_namespace_v1.typesense.metadata[0].name
  description = "Kubernetes namespace where Typesense runs."
}

output "typesense_service_name" {
  value       = kubernetes_service_v1.typesense.metadata[0].name
  description = "ClusterIP service name for Typesense API traffic."
}

output "typesense_api_key_secret_arn" {
  value       = aws_secretsmanager_secret.typesense_api_key.arn
  description = "Secrets Manager ARN containing the Typesense API key."
}

output "typesense_backup_bucket" {
  value       = aws_s3_bucket.typesense_backups.id
  description = "S3 bucket used for Typesense backups."
}

output "typesense_dashboard_name" {
  value       = aws_cloudwatch_dashboard.typesense.dashboard_name
  description = "CloudWatch dashboard for Typesense health and latency." 
}
