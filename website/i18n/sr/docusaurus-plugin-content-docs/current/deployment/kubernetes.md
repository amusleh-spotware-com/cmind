---
description: "Helm шарта: deploy/helm/cmind. Развој Web, MCP, самореги-страјућих агента чворова, опциона у-кластер Postgres."
---

# Kubernetes развој — корак по корак

Helm шарта: `deploy/helm/cmind`. Развој Web, MCP, самореги-страјућих агента чворова, опциона
у-кластер Postgres.

> **Валидиран** крај-краја на локалном `kind` кластеру: сви подови достигну `Ready`, агент чвора
> самореги-јује са по-подом headless DNS име, `/health` + `/version` враћају 200, скалиран-доле
> агент аутоматски означен недостижан. Ток доле = шта је тестирано.

## 0. Предуслови

- Kubernetes кластер (управљано EKS/AKS/GKE, или локални `kind`/`k3d`/`minikube`).
- `kubectl` (указивачи у циљни контекст) и `helm` 3.
- Регистар контејнера кластер може доставити (прескочи за локални `kind` — учитај слике уместо).

## 1. Грађење три слике

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Потисни (`docker push <registry>/cmind-web:1.0.0`, итд.), **или** за локални `kind` кластер учитај
директно:
