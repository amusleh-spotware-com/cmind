# S5: copy-trading agent on ECS Fargate. Hosts the CopyEngineSupervisor (App:Copy:Enabled=true) — long-lived
# trading sockets, no ALB (a worker, not a web tier). The DB connection string is stored in AWS Secrets
# Manager and injected through the task's `secrets` block, not as plaintext env. Each task's NodeName
# defaults to its container hostname (unique per Fargate task), so the supervisor lease attributes profiles
# per task and two tasks never double-host a profile. Scale `copy_agent_count` to add copy capacity; the
# DataProtection key ring is shared through Postgres, so any task can decrypt the stored Open API tokens.

resource "aws_secretsmanager_secret" "copy_connstr" {
  name_prefix = "${var.name_prefix}-copy-connstr-"
}

resource "aws_secretsmanager_secret_version" "copy_connstr" {
  secret_id     = aws_secretsmanager_secret.copy_connstr.id
  secret_string = local.connection_string
}

# Let the task execution role fetch the secret for `secrets` injection.
resource "aws_iam_role_policy" "copy_secrets" {
  name_prefix = "${var.name_prefix}-copy-secrets-"
  role        = aws_iam_role.exec.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["secretsmanager:GetSecretValue"]
      Resource = [aws_secretsmanager_secret.copy_connstr.arn]
    }]
  })
}

resource "aws_ecs_task_definition" "copy_agent" {
  family                   = "${var.name_prefix}-copy-agent"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = "512"
  memory                   = "1024"
  execution_role_arn       = aws_iam_role.exec.arn
  task_role_arn            = aws_iam_role.task.arn
  container_definitions = jsonencode([
    {
      name      = "copy-agent"
      image     = "${var.image_registry}-web:${var.image_tag}"
      essential = true
      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "App__Features__CopyTrading", value = "true" },
        { name = "App__Copy__Enabled", value = "true" },
        { name = "OTEL_EXPORTER_OTLP_ENDPOINT", value = "http://localhost:4317" }
      ]
      secrets = [
        { name = "ConnectionStrings__appdb", valueFrom = aws_secretsmanager_secret.copy_connstr.arn }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.this.name
          "awslogs-region"        = var.region
          "awslogs-stream-prefix" = "copy-agent"
        }
      }
    },
    local.adot_container
  ])
}

resource "aws_ecs_service" "copy_agent" {
  name            = "${var.name_prefix}-copy-agent"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.copy_agent.arn
  desired_count   = var.copy_agent_count
  launch_type     = "FARGATE"
  network_configuration {
    subnets          = data.aws_subnets.default.ids
    security_groups  = [aws_security_group.svc.id]
    assign_public_ip = true
  }
}
