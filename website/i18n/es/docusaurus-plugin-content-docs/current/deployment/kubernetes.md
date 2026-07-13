---
description: "Gráfico de Helm: deploy/helm/cmind. Despliegua Web, MCP, agentes de nodo de auto-registro, Postgres en clúster opcional."
---

# Despliegue de Kubernetes — paso a paso

Gráfico de Helm: `deploy/helm/cmind`. Despliegua Web, MCP, agentes de nodo de auto-registro, Postgres en clúster
opcional.

> **Validado** de extremo a extremo en clúster local `kind`: todos los pods llegan a `Ready`, agente de nodo
> se auto-registra con nombre DNS sin cabeza por pod, `/health` + `/version` devuelven 200, agente escalado hacia abajo
> marcado como no alcanzable. El flujo abajo = lo que se probó.

## 0. Requisitos previos

- Clúster de Kubernetes (EKS/AKS/GKE administrado, o local `kind`/`k3d`/`minikube`).
- `kubectl` (apuntado al contexto de destino) y `helm` 3.
- Registro de contenedor que el clúster puede extraer (omitir para `kind` local — en su lugar cargar imágenes).

## 1. Construir las tres imágenes

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Impulsa (`docker push <registry>/cmind-web:1.0.0`, etc.), **o** para clúster `kind` local carga
directo:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Elige secretos

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 caracteres; secreto de clúster compartido para auto-descubrimiento de nodos
```

## 3. Instala el gráfico

Basado en registro (clúster administrado):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Local `kind` (imágenes cargadas, sin Postgres externo, agentes sin privilegios):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> En `kind`/containerd sin socket Docker del host, así que `web.dockerSocket.enabled=false`
> (compilador en aplicación/LocalNode no disponible) y `nodeAgent.privileged=false` (agente aún
> **se auto-registra**; solo no puede ejecutar contenedores de cTrader sin DinD). Para ejecución de carga de trabajo real,
> ejecuta agentes en grupo de nodos donde `nodeAgent.privileged=true` está permitido.

¿Sin binario `helm`? Renderiza y aplica:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Espera a rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Espera: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) y `cmind-node-agent-0`
(StatefulSet) todos `Ready`. La preparación web (`/health`) pasa solo una vez que la BD se migra (las migraciones
se ejecutan al iniciar).

## 5. Verifica auto-descubrimiento

```bash
# El agente de nodo debe aparecer en la BD con un nombre DNS sin cabeza por pod BaseUrl e IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Ejemplo (verificado):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Escala capacidad agregando réplicas — cada nuevo pod se auto-registra dentro de un intervalo de latido:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Reconciliación de obsolescencia (verificada): escala el agente hacia abajo, cambia a `IsReachable=f` después de
`discovery.heartbeatTtl`; escala hacia arriba, vuelve en línea.

## 6. Alcanza la interfaz de usuario

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — inicia sesión con el propietario sembrado
```

Acceso externo: establecer `web.ingress.enabled=true`, `web.ingress.host` y TLS.

## Por qué los agentes de nodo son un StatefulSet

El distribuidor de nodo principal envía trabajo a un agente **específico** por URL, por lo que cada agente necesita un nombre DNS estable
individualmente direccionable. El gráfico usa StatefulSet + servicio sin cabeza; cada pod
anuncia `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` y se auto-registra bajo el nombre del pod.
El mismo mecanismo de descubrimiento que usan los nodos CLI de cTrader desnudos —
ver [../operations/node-discovery.md](../operations/node-discovery.md).

## Escala web (plano posterior de SignalR, S6)

Aplicación web = Blazor Server + SignalR (panel en vivo, centro de registros). Para ejecutar **más de una réplica web**,
establece la cadena de conexión `signalr` en el punto final de Redis — la aplicación se registra **SignalR Redis
plano posterior** (`AddStackExchangeRedis`) para que los mensajes del centro y la negociación de circuitos se distribuyan entre réplicas y una
reconexión que aterriza en un pod diferente permanece en vivo. Sin cadena de conexión `signalr` = única réplica
en memoria (sin cambios). Empareja con afinidad de sesión en entrada para circuitos Blazor Server más suave.

## Escalado automático y resiliencia del agente de copia

Los agentes de copia alojan sockets comerciales de larga duración, por lo que se escalan en **trabajo, no CPU**. Con
`copyAgent.keda.enabled=true` el gráfico instala KEDA `ScaledObject` que consulta Postgres para
conteo de perfil de copia en ejecución y escala réplicas para que cada pod aloje aproximadamente `copyAgent.keda.profilesPerPod`
(predeterminado 25), entre `minReplicas`/`maxReplicas`. KEDA lee BD a través de `TriggerAuthentication` vinculada a
clave secreta `copyAgent.keda.connectionSecretKey`. Cuando `copyAgent.replicas > 1` (o KEDA escala más de 1)
el gráfico también agrega `topologySpreadConstraints` (distribuir entre nodos) y `PodDisruptionBudget`
(`minAvailable: 1`); en escala hacia abajo / actualización gradual cada pod libera arrendamientos en `SIGTERM`
