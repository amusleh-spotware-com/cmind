---
description: "deploy/azure/main.bicep aprovisionamientos capa sin estado en Azure Container Apps más Postgres Flexible Server + Log Analytics."
---

# Despliegue de Azure — paso a paso

`deploy/azure/main.bicep` aprovisiona capa sin estado en **Azure Container Apps** más **Postgres Flexible Server** + Log Analytics.

## 1. Requisitos previos

- CLI de Azure (`az login` completado), suscripción, permiso para crear grupos de recursos.
- Tres imágenes impulsadas al registro que Azure puede extraer (ej. GHCR público o ACR).

## 2. Crea un grupo de recursos

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Despliegua el Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Crea: Ambiente de Container Apps, Web (ingreso externo), MCP (ingreso externo), Postgres Flexible Server + `appdb`, Log Analytics, 
componente **Application Insights basado en área de trabajo**. Descubrimiento activado para Web. Su cadena de conexión inyectada en Web + MCP como 
`APPLICATIONINSIGHTS_CONNECTION_STRING`, por lo que trazas + métricas se exportan de forma nativa a App Insights mientras los registros aterrizan en el mismo 
espacio de trabajo de Log Analytics — sin recolector necesario. Pasar `-p otlpEndpoint=...` para *también* reenviar a recolector OTLP.

## 4. Obtén las URL

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Abre `webUrl`, inicia sesión con propietario (cambio de contraseña forzado en el primer inicio de sesión).

## 5. Agrega agentes de nodo (separado)

Container Apps no puede ejecutar privilegiado/DinD, así que ejecuta agentes en otro lugar, apunta a `webUrl`:

- **AKS** — despliegua gráfico de Helm ([kubernetes.md](kubernetes.md)) con `nodeAgent.privileged=true`, escala web/MCP a 0 si quieres solo nivel de agente allí.
- **VM / VMSS** — ejecuta imagen `cmind-node-agent` con `--privileged` con `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Los agentes se auto-registran dentro de un intervalo de latido — ver [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Verifica

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # registros JSON compactos
curl -s <webUrl>/version
```

## Notas de producción

- Web frontal con Azure Front Door / App Gateway para TLS + WAF.
- Almacena secretos en Key Vault; pasa certificado de protección de datos estable (`App__DataProtectionCertBase64` / `...Password`) 
  para que el anillo de claves sobreviva a reinicios de réplica.
- Application Insights (trazas+métricas) + Log Analytics (registros) alambrados automáticamente; correlaciona en `trace_id`. 
  Ver [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Establece parámetro `otlpEndpoint` (o `OTEL_EXPORTER_OTLP_ENDPOINT` en aplicaciones) para *también* reenviar a recolector.
- Las reglas de `scale` de Container Apps (min/max) están alambradas en Bicep.

## Agente de copia + Key Vault (S5)

`deploy/azure/main.bicep` también aprovisiona **agente de copia** Container App que aloja `CopyEngineSupervisor` 
(`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) sin **ingreso** — trabajador que aloja sockets de cTrader de larga duración. 
Lee la cadena de conexión BD desde secreto **Azure Key Vault** vía **identidad administrada asignada por el usuario** 
(rol de usuario de secretos de Key Vault) en lugar de secreto de texto sin formato en línea. La `NodeName` de cada réplica predeterminada 
a su nombre de host del contenedor (único), por lo que BD arrendamiento atributos perfiles en ejecución por réplica y dos réplicas nunca 
doble alojamiento uno. Escala `minReplicas`/`maxReplicas` para agregar capacidad de copia; el anillo de claves DataProtection se comparte 
a través de Postgres, por lo que cualquier réplica puede descifrar tokens de API abiertos almacenados. Salidas: `copyAgentName`, `keyVaultName`.
