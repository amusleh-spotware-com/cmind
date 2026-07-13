---
description: "deploy/aws = módulo Terraform: ECS Fargate (Web + MCP) detrás de ALB, RDS Postgres, registros de CloudWatch."
---

# Despliegue de AWS — paso a paso

`deploy/aws` = módulo Terraform: **ECS Fargate** (Web + MCP) detrás de **ALB**, **RDS Postgres**, registros de CloudWatch.

## 1. Requisitos previos

- Terraform ≥ 1.5 + credenciales de AWS (`aws configure` / variables de entorno) con derechos para crear recursos de VPC,
  ECS, RDS, ALB, IAM.
- Tres imágenes en registro que ECS puede extraer (ECR o GHCR público).

## 2. Inicializa

```bash
cd deploy/aws
terraform init
```

## 3. Aplica

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Crea: RDS Postgres (`appdb`), clúster ECS, servicios Fargate para Web + MCP, ALB (Web en `/`,
MCP en `/mcp`), grupos de seguridad, grupo de registros de CloudWatch, **recolector ADOT (AWS Distro para
OpenTelemetry)** en cada tarea. La aplicación exporta OTLP al colector, que envía
trazas a **X-Ray**, métricas a **CloudWatch** (EMF, espacio de nombres `cmind`); registros permanecen en
controlador `awslogs` como JSON compacto. Descubrimiento activado para Web. El rol de tarea otorga acceso de escritura al colector
X-Ray + CloudWatch — sin recolector para ejecutar tú mismo.

> Utiliza **VPC predeterminada/subredes** de la cuenta por brevedad. Para producción, alambra tu propia VPC, subredes
> privadas, receptor HTTPS (certificado ACM).

## 4. Obtén las URL

```bash
terraform output web_url   # raíz de ALB
terraform output mcp_url   # ALB /mcp
```

Abre `web_url`, inicia sesión con propietario (cambio de contraseña forzado en el primer inicio de sesión).

## 5. Agrega agentes de nodo (separado)

Fargate deniega privilegiado/DinD, así que ejecuta agentes en otro lugar apuntando a `web_url`:

- **ECS on EC2** — proveedor de capacidad con definiciones de tarea `privileged = true` ejecutando
  `cmind-node-agent`.
- **EKS** — gráfico de Helm ([kubernetes.md](kubernetes.md)) con `nodeAgent.privileged=true`.

Establece `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`,
`NodeAgent__JwtSecret=<discovery_join_token>`. Los agentes se auto-registran — ver
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Verifica

```bash
aws logs tail /ecs/cmind --since 5m         # registros JSON compactos
curl -s "$(terraform output -raw web_url)/version"
```

## Notas de producción

- Agrega receptor HTTPS + certificado ACM; restringe el grupo de seguridad de ALB.
- Almacena secretos en AWS Secrets Manager / SSM, inyecta vía `secrets` de definición de tarea en lugar de
  `environment` de texto sin formato.
- Habilita multi-AZ de RDS + copias de seguridad.
- Trazas (X-Ray), métricas (CloudWatch EMF), registros (CloudWatch Logs) alambrados automáticamente vía
  colector ADOT; correlaciona en `trace_id`. Ver
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- La aplicación ya apunta `OTEL_EXPORTER_OTLP_ENDPOINT` al colector en tarea; reapunta a
  recolector externo si prefieres centralizar.

## Agente de copia + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` agrega servicio **copia-agente** ECS Fargate que aloja `CopyEngineSupervisor`
(`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) sin **ALB** — trabajador que aloja sockets de cTrader de larga duración. 
Cadena de conexión BD almacenada en **AWS Secrets Manager**, inyectada a través del bloque `secrets` de tarea 
(rol de ejecución otorgado `secretsmanager:GetSecretValue` solo en ese secreto), no env de texto sin formato. 
La `NodeName` de cada tarea predeterminada a su nombre de host del contenedor (único por tarea de Fargate), 
por lo que arrendamiento de BD atributos perfiles en ejecución por tarea — dos tareas nunca doble alojamiento uno. 
Escala `copy_agent_count` para agregar capacidad de copia; el anillo de claves DataProtection se comparte a través de Postgres, 
por lo que cualquier tarea puede descifrar tokens de API abiertos almacenados.
