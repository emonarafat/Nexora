environment            = "prod"
aws_region             = "us-east-1"
eks_cluster_name       = "nexora-shared-eks-prod"
eks_oidc_provider_arn  = "arn:aws:iam::123456789012:oidc-provider/oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE"
eks_oidc_issuer_url    = "https://oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE"
typesense_namespace    = "nexora-prod"
backup_bucket_name     = "nexora-typesense-prod-backups"
node_storage_size      = "200Gi"
storage_class_name     = "gp3"
allowed_namespace_labels = {
  "kubernetes.io/metadata.name" = "nexora-prod"
}
tags = {
  Project = "Nexora"
  Owner   = "SearchPlatform"
}
