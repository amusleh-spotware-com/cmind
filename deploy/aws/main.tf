# cmind on AWS: ECS Fargate (Web + MCP) + RDS Postgres + ALB.
# Node agents need privileged Docker (DinD), which Fargate does not allow. Run them on an ECS
# EC2 capacity provider (privileged=true) or EKS (deploy/helm), pointing NodeAgent__MainUrl at the
# Web ALB URL. This module provisions the stateless Web/MCP tier and the database.

terraform {
  required_version = ">= 1.5"
  required_providers {
    aws = { source = "hashicorp/aws", version = "~> 5.0" }
  }
}

provider "aws" {
  region = var.region
}

data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
}

locals {
  connection_string = "Host=${aws_db_instance.pg.address};Port=5432;Database=appdb;Username=cmindadmin;Password=${var.pg_password};SSL Mode=Require;Trust Server Certificate=true"

  # ADOT collector config: receive OTLP from the app on localhost, ship traces to X-Ray and
  # metrics to CloudWatch (EMF). Logs stay on the awslogs driver (compact JSON -> Logs Insights).
  adot_config = <<-YAML
    receivers:
      otlp:
        protocols:
          grpc:
            endpoint: 0.0.0.0:4317
          http:
            endpoint: 0.0.0.0:4318
    processors:
      batch:
    exporters:
      awsxray:
      awsemf:
        namespace: cmind
        log_group_name: /ecs/${var.name_prefix}/metrics
    service:
      pipelines:
        traces:
          receivers: [otlp]
          processors: [batch]
          exporters: [awsxray]
        metrics:
          receivers: [otlp]
          processors: [batch]
          exporters: [awsemf]
  YAML
}

# Reusable ADOT collector sidecar definition. Sits in the same task as an app container; the app
# reaches it over localhost (awsvpc network mode shares the loopback).
locals {
  adot_container = {
    name      = "adot"
    image     = "public.ecr.aws/aws-observability/aws-otel-collector:latest"
    essential = false
    environment = [
      { name = "AOT_CONFIG_CONTENT", value = local.adot_config }
    ]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = aws_cloudwatch_log_group.this.name
        "awslogs-region"        = var.region
        "awslogs-stream-prefix" = "adot"
      }
    }
  }
}

# ---------------- Security groups ----------------

resource "aws_security_group" "alb" {
  name_prefix = "${var.name_prefix}-alb-"
  vpc_id      = data.aws_vpc.default.id
  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "svc" {
  name_prefix = "${var.name_prefix}-svc-"
  vpc_id      = data.aws_vpc.default.id
  ingress {
    from_port       = 8080
    to_port         = 8080
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "db" {
  name_prefix = "${var.name_prefix}-db-"
  vpc_id      = data.aws_vpc.default.id
  ingress {
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.svc.id]
  }
}

# ---------------- Database ----------------

resource "aws_db_instance" "pg" {
  identifier             = "${var.name_prefix}-pg"
  engine                 = "postgres"
  engine_version         = "16"
  instance_class         = "db.t4g.micro"
  allocated_storage      = 20
  db_name                = "appdb"
  username               = "cmindadmin"
  password               = var.pg_password
  vpc_security_group_ids = [aws_security_group.db.id]
  skip_final_snapshot    = true
  publicly_accessible    = false
}

# ---------------- ECS ----------------

resource "aws_ecs_cluster" "this" {
  name = "${var.name_prefix}-cluster"
}

resource "aws_cloudwatch_log_group" "this" {
  name              = "/ecs/${var.name_prefix}"
  retention_in_days = 30
}

resource "aws_iam_role" "exec" {
  name_prefix        = "${var.name_prefix}-exec-"
  assume_role_policy = jsonencode({
    Version   = "2012-10-17"
    Statement = [{ Action = "sts:AssumeRole", Effect = "Allow", Principal = { Service = "ecs-tasks.amazonaws.com" } }]
  })
}

resource "aws_iam_role_policy_attachment" "exec" {
  role       = aws_iam_role.exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# Task role — grants the ADOT collector sidecar rights to ship traces to X-Ray and metrics to
# CloudWatch (EMF). The app containers export OTLP to the sidecar on localhost.
resource "aws_iam_role" "task" {
  name_prefix        = "${var.name_prefix}-task-"
  assume_role_policy = jsonencode({
    Version   = "2012-10-17"
    Statement = [{ Action = "sts:AssumeRole", Effect = "Allow", Principal = { Service = "ecs-tasks.amazonaws.com" } }]
  })
}

resource "aws_iam_role_policy_attachment" "task_xray" {
  role       = aws_iam_role.task.name
  policy_arn = "arn:aws:iam::aws:policy/AWSXRayDaemonWriteAccess"
}

resource "aws_iam_role_policy_attachment" "task_cw" {
  role       = aws_iam_role.task.name
  policy_arn = "arn:aws:iam::aws:policy/CloudWatchAgentServerPolicy"
}

resource "aws_lb" "this" {
  name               = "${var.name_prefix}-alb"
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = data.aws_subnets.default.ids
}

# Web

resource "aws_lb_target_group" "web" {
  name        = "${var.name_prefix}-web"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = data.aws_vpc.default.id
  target_type = "ip"
  health_check {
    path    = "/health"
    matcher = "200"
  }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.this.arn
  port              = 80
  protocol          = "HTTP"
  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.web.arn
  }
}

resource "aws_lb_target_group" "mcp" {
  name        = "${var.name_prefix}-mcp"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = data.aws_vpc.default.id
  target_type = "ip"
  health_check {
    path    = "/version"
    matcher = "200"
  }
}

resource "aws_lb_listener_rule" "mcp" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 10
  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.mcp.arn
  }
  condition {
    path_pattern { values = ["/mcp", "/mcp/*"] }
  }
}

resource "aws_ecs_task_definition" "web" {
  family                   = "${var.name_prefix}-web"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = "1024"
  memory                   = "2048"
  execution_role_arn       = aws_iam_role.exec.arn
  task_role_arn            = aws_iam_role.task.arn
  container_definitions = jsonencode([
    {
      name      = "web"
      image     = "${var.image_registry}-web:${var.image_tag}"
      essential = true
      portMappings = [{ containerPort = 8080 }]
      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "ConnectionStrings__appdb", value = local.connection_string },
        { name = "App__OwnerEmail", value = var.owner_email },
        { name = "App__OwnerPassword", value = var.owner_password },
        { name = "App__Discovery__Enabled", value = "true" },
        { name = "App__Discovery__JoinToken", value = var.discovery_join_token },
        { name = "OTEL_EXPORTER_OTLP_ENDPOINT", value = "http://localhost:4317" }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.this.name
          "awslogs-region"        = var.region
          "awslogs-stream-prefix" = "web"
        }
      }
    },
    local.adot_container
  ])
}

resource "aws_ecs_task_definition" "mcp" {
  family                   = "${var.name_prefix}-mcp"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = "512"
  memory                   = "1024"
  execution_role_arn       = aws_iam_role.exec.arn
  task_role_arn            = aws_iam_role.task.arn
  container_definitions = jsonencode([
    {
      name      = "mcp"
      image     = "${var.image_registry}-mcp:${var.image_tag}"
      essential = true
      portMappings = [{ containerPort = 8080 }]
      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "ConnectionStrings__appdb", value = local.connection_string },
        { name = "OTEL_EXPORTER_OTLP_ENDPOINT", value = "http://localhost:4317" }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.this.name
          "awslogs-region"        = var.region
          "awslogs-stream-prefix" = "mcp"
        }
      }
    },
    local.adot_container
  ])
}

resource "aws_ecs_service" "web" {
  name            = "${var.name_prefix}-web"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.web.arn
  desired_count   = 2
  launch_type     = "FARGATE"
  network_configuration {
    subnets          = data.aws_subnets.default.ids
    security_groups  = [aws_security_group.svc.id]
    assign_public_ip = true
  }
  load_balancer {
    target_group_arn = aws_lb_target_group.web.arn
    container_name   = "web"
    container_port   = 8080
  }
  depends_on = [aws_lb_listener.http]
}

resource "aws_ecs_service" "mcp" {
  name            = "${var.name_prefix}-mcp"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.mcp.arn
  desired_count   = 1
  launch_type     = "FARGATE"
  network_configuration {
    subnets          = data.aws_subnets.default.ids
    security_groups  = [aws_security_group.svc.id]
    assign_public_ip = true
  }
  load_balancer {
    target_group_arn = aws_lb_target_group.mcp.arn
    container_name   = "mcp"
    container_port   = 8080
  }
  depends_on = [aws_lb_listener_rule.mcp]
}

output "web_url" {
  value = "http://${aws_lb.this.dns_name}"
}

output "mcp_url" {
  value = "http://${aws_lb.this.dns_name}/mcp"
}
