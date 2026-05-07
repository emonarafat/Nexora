variable "environment" {
  description = "Deployment environment (dev, staging, prod)."
  type        = string

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "environment must be one of: dev, staging, prod."
  }
}

variable "aws_region" {
  description = "AWS region where the EKS cluster is provisioned."
  type        = string
  default     = "us-east-1"
}

variable "eks_cluster_name" {
  description = "Shared EKS cluster name where Typesense runs."
  type        = string
}

variable "eks_oidc_provider_arn" {
  description = "IAM OIDC provider ARN for the EKS cluster (IRSA)."
  type        = string
}

variable "eks_oidc_issuer_url" {
  description = "OIDC issuer URL for the EKS cluster."
  type        = string
}

variable "typesense_namespace" {
  description = "Namespace where Typesense resources are deployed."
  type        = string
  default     = "nexora-search"
}

variable "typesense_image" {
  description = "Typesense image tag for stateful cluster nodes."
  type        = string
  default     = "typesense/typesense:27.1"
}

variable "typesense_api_key_secret_name" {
  description = "Secrets Manager secret name that stores Typesense API key."
  type        = string
  default     = ""
}

variable "typesense_api_key_rotation_lambda_arn" {
  description = "Optional Lambda ARN for Secrets Manager rotation."
  type        = string
  default     = null
}

variable "backup_bucket_name" {
  description = "S3 bucket used for Typesense snapshot backups."
  type        = string
}

variable "storage_class_name" {
  description = "StorageClass for Typesense PVCs."
  type        = string
  default     = "gp3"
}

variable "node_storage_size" {
  description = "Persistent storage size per Typesense node."
  type        = string
  default     = "100Gi"
}

variable "allowed_namespace_labels" {
  description = "Namespace labels allowed to access Typesense service in-cluster."
  type        = map(string)
  default = {
    "kubernetes.io/metadata.name" = "nexora"
  }
}

variable "tags" {
  description = "Tags to apply to AWS resources."
  type        = map(string)
  default     = {}
}

variable "snapshot_schedule" {
  description = "Cron schedule for hourly snapshot uploads to S3."
  type        = string
  default     = "0 * * * *"
}

variable "restore_test_schedule" {
  description = "Cron schedule for weekly restore validation checks."
  type        = string
  default     = "0 4 * * 0"
}
