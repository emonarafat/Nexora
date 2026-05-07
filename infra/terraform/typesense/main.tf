locals {
  name_prefix   = "nexora-typesense-${var.environment}"
  secret_name   = var.typesense_api_key_secret_name != "" ? var.typesense_api_key_secret_name : "/nexora/${var.environment}/typesense/api-key"
  service_name  = "typesense"
  cluster_nodes = [
    "typesense-0.typesense-headless.${var.typesense_namespace}.svc.cluster.local:8107:8108",
    "typesense-1.typesense-headless.${var.typesense_namespace}.svc.cluster.local:8107:8108",
    "typesense-2.typesense-headless.${var.typesense_namespace}.svc.cluster.local:8107:8108"
  ]
  tags = merge(var.tags, {
    Environment = var.environment
    Service     = "typesense"
    ManagedBy   = "terraform"
  })
}

resource "random_password" "typesense_api_key" {
  length  = 48
  special = false
}

resource "aws_secretsmanager_secret" "typesense_api_key" {
  name        = local.secret_name
  description = "Typesense API key for ${var.environment}."
  tags        = local.tags
}

resource "aws_secretsmanager_secret_version" "typesense_api_key" {
  secret_id = aws_secretsmanager_secret.typesense_api_key.id
  secret_string = jsonencode({
    apiKey = random_password.typesense_api_key.result
  })
}

resource "aws_secretsmanager_secret_rotation" "typesense_api_key" {
  count = var.typesense_api_key_rotation_lambda_arn == null ? 0 : 1

  secret_id           = aws_secretsmanager_secret.typesense_api_key.id
  rotation_lambda_arn = var.typesense_api_key_rotation_lambda_arn

  rotation_rules {
    automatically_after_days = 30
  }
}

resource "aws_s3_bucket" "typesense_backups" {
  bucket = var.backup_bucket_name
  tags   = local.tags
}

resource "aws_s3_bucket_versioning" "typesense_backups" {
  bucket = aws_s3_bucket.typesense_backups.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "typesense_backups" {
  bucket = aws_s3_bucket.typesense_backups.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_iam_role" "typesense_service_account" {
  name = "${local.name_prefix}-irsa"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Principal = {
        Federated = var.eks_oidc_provider_arn
      }
      Action = "sts:AssumeRoleWithWebIdentity"
      Condition = {
        StringEquals = {
          "${replace(var.eks_oidc_issuer_url, "https://", "")}:sub" = "system:serviceaccount:${var.typesense_namespace}:typesense"
          "${replace(var.eks_oidc_issuer_url, "https://", "")}:aud" = "sts.amazonaws.com"
        }
      }
    }]
  })
  tags = local.tags
}

resource "aws_iam_role_policy" "typesense_service_account" {
  name = "${local.name_prefix}-policy"
  role = aws_iam_role.typesense_service_account.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "ReadTypesenseApiSecret"
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue",
          "secretsmanager:DescribeSecret"
        ]
        Resource = aws_secretsmanager_secret.typesense_api_key.arn
      },
      {
        Sid    = "WriteBackups"
        Effect = "Allow"
        Action = [
          "s3:PutObject",
          "s3:GetObject",
          "s3:ListBucket"
        ]
        Resource = [
          aws_s3_bucket.typesense_backups.arn,
          "${aws_s3_bucket.typesense_backups.arn}/*"
        ]
      },
      {
        Sid    = "CloudWatchMetrics"
        Effect = "Allow"
        Action = [
          "cloudwatch:PutMetricData"
        ]
        Resource = "*"
      }
    ]
  })
}

resource "kubernetes_namespace_v1" "typesense" {
  metadata {
    name = var.typesense_namespace
    labels = {
      "app.kubernetes.io/part-of" = "nexora"
      "app.kubernetes.io/name"    = "typesense"
      "environment"               = var.environment
    }
  }
}

resource "kubernetes_service_account_v1" "typesense" {
  metadata {
    name      = "typesense"
    namespace = kubernetes_namespace_v1.typesense.metadata[0].name
    annotations = {
      "eks.amazonaws.com/role-arn" = aws_iam_role.typesense_service_account.arn
    }
  }
}

resource "kubernetes_secret_v1" "typesense" {
  metadata {
    name      = "typesense-auth"
    namespace = kubernetes_namespace_v1.typesense.metadata[0].name
  }

  data = {
    "api-key" = random_password.typesense_api_key.result
  }

  type = "Opaque"
}

resource "kubernetes_service_v1" "typesense_headless" {
  metadata {
    name      = "typesense-headless"
    namespace = kubernetes_namespace_v1.typesense.metadata[0].name
    labels = {
      app = "typesense"
    }
  }

  spec {
    cluster_ip = "None"
    selector = {
      app = "typesense"
    }

    port {
      name        = "http"
      port        = 8108
      target_port = 8108
    }

    port {
      name        = "peering"
      port        = 8107
      target_port = 8107
    }
  }
}

resource "kubernetes_service_v1" "typesense" {
  metadata {
    name      = local.service_name
    namespace = kubernetes_namespace_v1.typesense.metadata[0].name
    labels = {
      app = "typesense"
    }
  }

  spec {
    type = "ClusterIP"
    selector = {
      app = "typesense"
    }

    port {
      name        = "http"
      port        = 8108
      target_port = 8108
    }
  }
}

resource "kubernetes_stateful_set_v1" "typesense" {
  metadata {
    name      = "typesense"
    namespace = kubernetes_namespace_v1.typesense.metadata[0].name
    labels = {
      app = "typesense"
    }
  }

  spec {
    service_name = kubernetes_service_v1.typesense_headless.metadata[0].name
    replicas     = 3

    selector {
      match_labels = {
        app = "typesense"
      }
    }

    template {
      metadata {
        labels = {
          app = "typesense"
        }
      }

      spec {
        service_account_name = kubernetes_service_account_v1.typesense.metadata[0].name

        affinity {
          pod_anti_affinity {
            required_during_scheduling_ignored_during_execution {
              label_selector {
                match_expressions {
                  key      = "app"
                  operator = "In"
                  values   = ["typesense"]
                }
              }
              topology_key = "kubernetes.io/hostname"
            }
          }
        }

        container {
          name              = "typesense"
          image             = var.typesense_image
          image_pull_policy = "IfNotPresent"

          args = [
            "--data-dir=/data",
            "--api-key=$(TYPESENSE_API_KEY)",
            "--listen-port=8108",
            "--peering-port=8107",
            "--nodes=${join(",", local.cluster_nodes)}"
          ]

          env {
            name = "TYPESENSE_API_KEY"
            value_from {
              secret_key_ref {
                name = kubernetes_secret_v1.typesense.metadata[0].name
                key  = "api-key"
              }
            }
          }

          port {
            name           = "http"
            container_port = 8108
          }

          port {
            name           = "peering"
            container_port = 8107
          }

          resources {
            limits = {
              cpu    = "4"
              memory = "8Gi"
            }
            requests = {
              cpu    = "4"
              memory = "8Gi"
            }
          }

          volume_mount {
            name       = "typesense-data"
            mount_path = "/data"
          }

          readiness_probe {
            http_get {
              path = "/health"
              port = 8108
            }
            initial_delay_seconds = 15
            period_seconds        = 10
          }

          liveness_probe {
            http_get {
              path = "/health"
              port = 8108
            }
            initial_delay_seconds = 30
            period_seconds        = 15
          }
        }
      }
    }

    volume_claim_template {
      metadata {
        name = "typesense-data"
      }

      spec {
        access_modes       = ["ReadWriteOnce"]
        storage_class_name = var.storage_class_name

        resources {
          requests = {
            storage = var.node_storage_size
          }
        }
      }
    }
  }
}

resource "kubernetes_network_policy_v1" "typesense" {
  metadata {
    name      = "typesense-private-access"
    namespace = kubernetes_namespace_v1.typesense.metadata[0].name
  }

  spec {
    pod_selector {
      match_labels = {
        app = "typesense"
      }
    }

    ingress {
      from {
        namespace_selector {
          match_labels = var.allowed_namespace_labels
        }
      }

      ports {
        port     = "8108"
        protocol = "TCP"
      }
      ports {
        port     = "8107"
        protocol = "TCP"
      }
    }

    policy_types = ["Ingress"]
  }
}

resource "kubernetes_pod_disruption_budget_v1" "typesense" {
  metadata {
    name      = "typesense-pdb"
    namespace = kubernetes_namespace_v1.typesense.metadata[0].name
  }

  spec {
    min_available = "2"

    selector {
      match_labels = {
        app = "typesense"
      }
    }
  }
}

resource "kubernetes_cron_job_v1" "typesense_snapshot_backup" {
  metadata {
    name      = "typesense-snapshot-backup"
    namespace = kubernetes_namespace_v1.typesense.metadata[0].name
  }

  spec {
    schedule                      = var.snapshot_schedule
    successful_jobs_history_limit = 2
    failed_jobs_history_limit     = 3

    job_template {
      metadata {}

      spec {
        template {
          metadata {}

          spec {
            restart_policy       = "OnFailure"
            service_account_name = kubernetes_service_account_v1.typesense.metadata[0].name

            container {
              name  = "snapshot-backup"
              image = "alpine:3.20"
              env {
                name = "TYPESENSE_API_KEY"
                value_from {
                  secret_key_ref {
                    name = kubernetes_secret_v1.typesense.metadata[0].name
                    key  = "api-key"
                  }
                }
              }
              command = [
                "/bin/sh",
                "-c",
                <<-EOT
                  set -euo pipefail
                  apk add --no-cache curl aws-cli >/dev/null
                  TS="$(date -u +%Y%m%dT%H%M%SZ)"
                  curl -fsS -X POST "http://${local.service_name}.${var.typesense_namespace}.svc.cluster.local:8108/operations/snapshot" \
                    -H "X-TYPESENSE-API-KEY:$${TYPESENSE_API_KEY}" \
                    -o "/tmp/snapshot-${TS}.json"
                  aws s3 cp "/tmp/snapshot-${TS}.json" "s3://${aws_s3_bucket.typesense_backups.id}/${var.environment}/snapshots/snapshot-${TS}.json"
                EOT
              ]
            }
          }
        }
      }
    }
  }
}

resource "kubernetes_cron_job_v1" "typesense_weekly_restore_test" {
  metadata {
    name      = "typesense-restore-test"
    namespace = kubernetes_namespace_v1.typesense.metadata[0].name
  }

  spec {
    schedule                      = var.restore_test_schedule
    successful_jobs_history_limit = 2
    failed_jobs_history_limit     = 3

    job_template {
      metadata {}

      spec {
        template {
          metadata {}

          spec {
            restart_policy       = "OnFailure"
            service_account_name = kubernetes_service_account_v1.typesense.metadata[0].name

            container {
              name  = "restore-test"
              image = "alpine:3.20"
              command = [
                "/bin/sh",
                "-c",
                <<-EOT
                  set -euo pipefail
                  apk add --no-cache aws-cli >/dev/null
                  LATEST=$(aws s3 ls "s3://${aws_s3_bucket.typesense_backups.id}/${var.environment}/snapshots/" | tail -n 1 | awk '{print $4}')
                  test -n "$LATEST"
                  aws s3 cp "s3://${aws_s3_bucket.typesense_backups.id}/${var.environment}/snapshots/$LATEST" /tmp/latest-snapshot.json
                EOT
              ]
            }
          }
        }
      }
    }
  }
}

resource "aws_cloudwatch_dashboard" "typesense" {
  dashboard_name = "${local.name_prefix}-dashboard"

  dashboard_body = jsonencode({
    widgets = [
      {
        type   = "metric"
        x      = 0
        y      = 0
        width  = 12
        height = 6
        properties = {
          title   = "Typesense Query Latency P95"
          view    = "timeSeries"
          region  = var.aws_region
          stat    = "p95"
          period  = 60
          metrics = [["Nexora/Typesense", "QueryLatencyMs", "Environment", var.environment]]
        }
      },
      {
        type   = "metric"
        x      = 12
        y      = 0
        width  = 12
        height = 6
        properties = {
          title   = "Typesense Replication Lag"
          view    = "timeSeries"
          region  = var.aws_region
          stat    = "Maximum"
          period  = 60
          metrics = [["Nexora/Typesense", "ReplicationLagSeconds", "Environment", var.environment]]
        }
      },
      {
        type   = "metric"
        x      = 0
        y      = 6
        width  = 24
        height = 6
        properties = {
          title   = "Typesense 5xx Error Rate"
          view    = "timeSeries"
          region  = var.aws_region
          stat    = "Sum"
          period  = 60
          metrics = [["Nexora/Typesense", "ServerErrors", "Environment", var.environment]]
        }
      }
    ]
  })
}

resource "aws_cloudwatch_metric_alarm" "typesense_query_latency" {
  alarm_name          = "${local.name_prefix}-query-latency-p95"
  alarm_description   = "P95 query latency exceeded threshold for Typesense."
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 3
  datapoints_to_alarm = 3
  metric_name         = "QueryLatencyMs"
  namespace           = "Nexora/Typesense"
  period              = 60
  extended_statistic  = "p95"
  threshold           = 100
  treat_missing_data  = "breaching"

  dimensions = {
    Environment = var.environment
  }

  tags = local.tags
}

resource "aws_cloudwatch_metric_alarm" "typesense_replication_lag" {
  alarm_name          = "${local.name_prefix}-replication-lag"
  alarm_description   = "Typesense replication lag exceeded 1 second."
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 3
  datapoints_to_alarm = 3
  metric_name         = "ReplicationLagSeconds"
  namespace           = "Nexora/Typesense"
  period              = 60
  statistic           = "Maximum"
  threshold           = 1
  treat_missing_data  = "breaching"

  dimensions = {
    Environment = var.environment
  }

  tags = local.tags
}

resource "aws_cloudwatch_metric_alarm" "typesense_error_rate" {
  alarm_name          = "${local.name_prefix}-error-rate"
  alarm_description   = "Typesense server errors detected."
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 2
  datapoints_to_alarm = 2
  metric_name         = "ServerErrors"
  namespace           = "Nexora/Typesense"
  period              = 60
  statistic           = "Sum"
  threshold           = 0
  treat_missing_data  = "notBreaching"

  dimensions = {
    Environment = var.environment
  }

  tags = local.tags
}
