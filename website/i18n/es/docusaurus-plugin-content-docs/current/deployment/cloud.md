---
title: Despliegúealo en la nube
description: Despliegua cMind en Azure, AWS o Kubernetes. Qué plataforma se ajusta, requisitos previos y guías paso a paso.
sidebar_position: 2
---

# Despliegúealo en la nube ☁️

¿Has crecido más allá de tu portátil? Es hora de poner cMind en infraestructura real. Buenas noticias: está diseñado para
escalar con casi ninguna ceremonia del operador — sin ZooKeeper, sin elección de líder, solo réplicas y una
base de datos.

**Lo único que debes saber de antemano:** la capa sin estado (Web + MCP) se ejecuta felizmente en *cualquier* contenedor
plataforma, pero **los agentes de nodo necesitan Docker privilegiado** (construyen y ejecutan contenedores de cTrader). Eso
descarta tiempos de ejecución sin servidor como Azure Container Apps y AWS Fargate para los *agentes* — ejecuta esos en
[Kubernetes](./kubernetes.md), una VM o EC2 y apúntales a tu URL web.

Elige tu camino:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — el gráfico de Helm, funciona en AKS / EKS / cualquier lugar.
- 📈 **[Escalado](./scaling.md)** — cómo se escala todo y se auto-sana una vez que está arriba.

La capa sin estado (Web + MCP) se ejecuta en cualquier plataforma de contenedor; Postgres = base de datos administrada.
**Los agentes de nodo necesitan Docker privilegiado (DinD)** — los tiempos de ejecución de contenedores sin servidor
(Azure Container Apps, AWS Fargate) lo bloquean. Ejecuta agentes en Kubernetes ([kubernetes.md](kubernetes.md)) o
VM/EC2, apunta a URL web.

| Nube | Capa sin estado | Base de datos | Guía |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Requisitos previos comunes, ambos:

1. Construir e impulsar tres imágenes al registro que la nube pueda extraer (`cmind-web`, `cmind-mcp`,
   `cmind-node-agent`).
2. Elige secretos: contraseña de BD, correo/contraseña del propietario, **token de unión de descubrimiento** (≥ 32 caracteres)
   compartido por aplicación web + cada agente de nodo.
3. Despliegua IaC (abajo), luego activa agentes de nodo por separado (K8s/VM) con
   `NodeAgent__MainUrl` = URL web desplegada, `NodeAgent__JwtSecret` = token de unión.

El descubrimiento, registro y sondeos se comportan igual que las configuraciones locales/K8s — ver
[../operations/node-discovery.md](../operations/node-discovery.md) y
[../operations/logging.md](../operations/logging.md).
