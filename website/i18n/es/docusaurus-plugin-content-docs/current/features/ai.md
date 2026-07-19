---
description: "cMind AI es agnóstico del proveedor — Anthropic, OpenAI, Azure OpenAI, Google Gemini y cualquier punto final compatible con OpenAI incluyendo modelos locales (Ollama, LM Studio, vLLM). Elige un proveedor, modelo y punto final; cada característica de IA funciona sin cambios."
---

# Características de IA

La capa de IA de cMind es **agnóstica del proveedor**. Cada característica habla con una costura neutral del proveedor única
(`IAiClient.CompleteAsync`); un **cliente de enrutamiento** resuelve la credencial activa del proveedor y envía
al adaptador de cable coincidente. Eliges un proveedor + modelo + punto final (y, si el proveedor lo necesita,
una clave); cada característica existente funciona sin cambios con el mismo cierre, encriptación, resiliencia y
degradación.

**Baterías incluidas:** un **LLM local integrado se envía con la aplicación y está habilitado por defecto**
(Microsoft.ML.OnnxRuntimeGenAI, ej. Phi-3-mini) — para que cada despliegue tenga IA funcionando **sin clave de API
y sin servicio externo**. Un despliegue de etiqueta blanca puede eliminarlo y restringir qué proveedores pueden
agregar los usuarios. Más allá del integrado, conecta cualquier proveedor externo.

Proveedores soportados:

- **IA local integrada** (`BuiltInOnnx`) — modelo ONNX GenAI en proceso, sin clave, enviado y predeterminado activado.
- **Anthropic** (Claude — API de Mensajes)
- **OpenAI** y **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Cualquier punto final compatible con OpenAI**, incluyendo **modelos locales** (Ollama, LM Studio, vLLM,
  `server` llama.cpp, LocalAI) y nubes compatibles con OpenAI (OpenRouter, Groq, Together, Mistral,
  DeepSeek) — todos a través del único adaptador compatible con OpenAI, diferenciándose solo por URL base + modelo + clave.

Exactamente **un** proveedor está activo a la vez. Las credenciales se almacenan **encriptadas**
(`AiProviderCredential` agregado + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
un punto final local **no necesita clave**. Sin **ningún** proveedor activo, cada característica devuelve el resultado deshabilitado
y el resto de la aplicación se ejecuta sin cambios (sin clave necesaria para compilar, probar o ejecutar la plataforma).

**Compatibilidad hacia atrás:** la antigua `App:Ai:ApiKey` de un despliegue existente (o la antigua configuración `ai.api_key`
encriptada) se honra automáticamente como un proveedor predeterminado **Anthropic** activo — sin acción necesaria.

IA no configurada → las páginas de IA atenúan acciones y muestran un banner más un mensaje único para agregar un proveedor en
**Configuración → IA** (`AiFeatureNotice`). Estado en `GET /api/ai/status` (`{ enabled, kind, model }`);
proveedores administrados (solo propietario) vía `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}` y un ping de conectividad `POST /api/ai/providers/test`.

## Predeterminado de despliegue vs proveedor propio del usuario

Las credenciales de IA tienen dos alcances:

- **Predeterminado de despliegue (administrado por propietario).** El propietario configura un proveedor (o envía uno vía
  `App:Ai:Providers[]` / el antiguo `App:Ai:ApiKey`). Se convierte en el **predeterminado compartido para cada usuario** —
  para que un corredor o proveedor de alojamiento pueda financiar IA para todos sus usuarios con **sin configuración por usuario y sin
  límite por usuario**. Administrado vía las rutas `/api/ai/providers` solo para propietarios anteriores.
- **Proveedor propio del usuario (autoservicio).** Cualquier usuario firmado puede agregar su propio proveedor bajo
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Cuando está presente, su **proveedor activo propio anula el
  predeterminado de despliegue para sus propias características de IA**; eliminarlo vuelve al predeterminado.

**Orden de resolución** (en `AiProviderStore`, por usuario de solicitud): la credencial activa propia del usuario → el
predeterminado de despliegue → la clave de configuración heredada → ninguno (IA deshabilitada). Exactamente una credencial está activa
**por alcance** (un índice único parcial por `OwnerUserId`) y cada alcance se resuelve independientemente, así que un
usuario activando su propia clave nunca perturba el predeterminado compartido. Contextos de fondo/no-Web (sin usuario de solicitud)
siempre resuelven el predeterminado de despliegue.

## Matriz de capacidad del proveedor

Las capacidades predeterminadas por proveedor y son anulables por propietario. Cuando una capacidad está desactivada la característica
**se degrada, nunca lanza**: la búsqueda web se descarta silenciosamente; la visión devuelve una falla
de tipo no compatible con capacidad.

| Proveedor | Tipo | URL base predeterminada | Clave requerida | Búsqueda web | Visión | Notas |
|---|---|---|---|---|---|---|
| IA local integrada | `BuiltInOnnx` | n/a (en proceso) | no | ✖ | ✖ | modelo ONNX GenAI enviado, predeterminado activado |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | sí | ✅ | ✅ | API de Mensajes, herramienta `web_search` |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | sí | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | sí | ✅ | ✅ | ruta de despliegue + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | sí | ✅ | ✅ | `generateContent`, fundamento `google_search` |
| Ollama (local) | `OpenAiCompatible` | `http://localhost:11434/v1/` | no | ✖ | dependiente del modelo | vía adaptador compatible con OpenAI |
| LM Studio (local) | `OpenAiCompatible` | `http://localhost:1234/v1/` | no | dependiente del modelo | dependiente del modelo | vía adaptador compatible con OpenAI |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | tu URL servida | no | ✖ | dependiente del modelo | vía adaptador compatible con OpenAI |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | URL del proveedor | sí | ✖ | dependiente del modelo | vía adaptador compatible con OpenAI |

Guías completas de configuración por proveedor (claves, URLs, ids de modelos, pasos de interfaz de usuario): ver
[Proveedores de IA — catálogo de configuración](../deployment/ai-providers.md).

## IA local integrada (enviada, predeterminado activado)

cMind envía un **LLM local real que se ejecuta en proceso** vía
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (un modelo instructivo compacto como
Phi-3.5-mini). No necesita **clave de API ni servicio externo**, y en el primer inicio — cuando ningún proveedor está
configurado y la puerta de etiqueta blanca lo permite — es **sembrado y activado automáticamente**, para que cada
despliegue tenga IA funcionando de la caja.

- El directorio del modelo (`genai_config.json` + tokenizador + pesos) está configurado por
  `App:Ai:BuiltIn:ModelPath` (predeterminado `models/onnx`, relativo al directorio base de la aplicación). Cuando los archivos del modelo
  están ausentes el proveedor **se degrada a una falla tipificada con una pista de instalación** — nunca lanza,
  y el resto de la aplicación no se ve afectada.
- Potencia cada característica de IA de texto. Siendo un modelo compacto, es solo texto (sin búsqueda web del lado del servidor o
  visión) y la generación es serializada (una instancia de modelo, reutilizada después de una carga perezosa).
- **Múltiples modelos integrados pueden coexistir.** Cada modelo descargado vive bajo `ModelPath/<key>`; un catálogo curado (Phi-3.5-mini predeterminado, más Phi-3-mini-128k) puede descargarse e intercambiarse desde **Configuración → IA**. Seleccionar un submodelo integrado lo carga en proceso. Adquirir/agrupar un modelo: ver [Proveedores de IA → integrado](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## Controles de etiqueta blanca

Un despliegue de etiqueta blanca restringe la IA vía `App:Branding` (aplicado del lado del servidor en cada upsert de proveedor):

- `AllowBuiltInAi` (predeterminado `true`) — establece `false` para **eliminar completamente el modelo integrado**.
- `AllowLocalProviders` (predeterminado `true`) — establece `false` para prohibir puntos finales locales/auto-alojados (loopback /
  OpenAI-compatible privado, ej. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (predeterminado vacío = todos) — enumera solo los tipos que el despliegue sanciona (ej.
  `["Anthropic","OpenAiCompatible"]`) para bloquear qué proveedores pueden agregar los usuarios.
- `AllowAiModelManagement` (predeterminado `true`) — establece `false` para ocultar **exploración de modelos**, el **selector de modelo por página**, y **vinculación de modelo por característica**. Todos son sintonizables por propietario en tiempo de ejecución desde **Configuración → Despliegue** (superpuestos en vivo en `IOptionsMonitor`) y catalogados en `WhiteLabelCatalog`.

## Extensión: futuros modelos integrados

La capa de IA es **basada en adaptadores y construida para crecer**. Cada proveedor es un `IAiProvider` seleccionado por
`AiProviderKind`; la costura orientada a características (`IAiClient`/`AiFeatureService`) nunca cambia. Agregar un nuevo
tiempo de ejecución de modelo integrado más tarde (otro modelo ONNX, un motor diferente en proceso, GGUF/llama.cpp
en proc, etc.) es un cambio localizado: agrega un `AiProviderKind`, implementa un adaptador `IAiProvider`,
regístralo y (opcionalmente) alambra la siembra predeterminada + una opción de diálogo — sin cambios de características, punto final o herramienta MCP. 
El proveedor ONNX integrado es la implementación de referencia de este patrón.

## Capacidades

- **Construir cBot** — mensaje en inglés simple → cBot ejecutable vía **generar → compilar → auto-reparación de IA** bucle (`build-strategy`), en `/ai/build`. El **código fuente generado se muestra** cuando la compilación finaliza (con un botón de copiar), junto al registro de compilación — en caso de éxito *y* en caso de fallo — para que siempre veas lo que escribió la IA, no solo errores.
- **Selección de modelo por página** — cada página y diálogo de característica de IA muestra un **selector de modelo** que enumera los modelos que puedes usar (tus propios proveedores + los predeterminados de despliegue). Preselecciona la vinculación guardada de la característica si está configurada, de lo contrario el modelo **predeterminado**, y el modelo que elijas se aplica a esa única acción (enviado como `?modelId=` y forzado por `RoutingAiClient` para esa llamada). Oculto cuando el despliegue deshabilita la administración de modelos.
- **Examinar y seleccionar modelos, por característica** — examina los modelos que un punto final de proveedor anuncia (`GET /v1/models` en LM Studio / Ollama / vLLM / llama.cpp, o el catálogo integrado) en lugar de escribir a mano una id, y **vincula cada característica de IA a un modelo diferente** para que varios modelos sirvan a diferentes características a la vez (una característica no vinculada regresa al proveedor predeterminado del alcance).
- **Optimización de parámetros** — bucle cerrado: IA propone conjuntos de parámetros, cada uno persiste + backtested en nodos (`optimize-run` / `optimize-params`).
- **Agente de cartera autónomo** — propuestas impulsadas por mandato con diario de decisión completo (`AgentMandate` → `AgentProposal`).
- **Guardia de riesgo actuante** — servicio de fondo `AiRiskGuard` evalúa bots en ejecución, puede **detener automáticamente** en riesgo crítico (opt-in).
- **Guardián de exposición de prop-firm** — límites de reducción/exposición con aplanamiento automático.
- **Alertas de mercado** — motor `AlertRule` con sentimiento de IA (fundado en búsqueda web donde el proveedor lo soporta).
- **Análisis** — revisión de cBot, análisis de backtest, análisis post-mortem, sentimiento de mercado, diseño de visión gráfica, curación de mercado.

## Superficies

- Puntos finales web bajo `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …). Cada punto final de característica acepta un `?modelId=<credential>` opcional para ejecutar esa única llamada en un modelo elegido. Plus **descubrimiento de modelos** (`/api/ai/models/probe`, `/api/ai/usable-models`) y **vinculaciones por característica** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- Herramientas MCP (`AiTools`) para clientes de IA — ver [mcp.md](mcp.md). La selección de proveedor es transparente para los clientes MCP.
- Grupo de navegación **IA** — una página Blazor **por característica**: Construir cBot (`/ai/build`), Revisión (`/ai/review`), Debate (`/ai/debate`), Sentimiento de Mercado (`/ai/sentiment`), Verificación de Exposición (`/ai/exposure`), Resumen de Cartera (`/ai/digest`), Asesor de Ajuste (`/ai/tune`), Optimizar (`/ai/optimize`), además de Agente de Cartera, Alertas, Claves MCP. Las páginas comparten `AiFeaturePageBase` + `AiOutputPanel` + un `AiModelSelect`; cada una muestra `AiFeatureNotice` cuando no hay proveedor configurado.
- **Configuración → IA** (`/settings/ai`, solo propietario) — lista de proveedores con un **diálogo Agregar / editar proveedor** (tipo, URL base con pistas por tipo incluyendo un preajuste localhost de Ollama/LM Studio, modelo, clave opcional, alternar capacidades, "establecer como predeterminado") y un botón **Probar conexión**.

## Configuración

`App:Ai` soporta tanto la clave única heredada como la siembra multi-proveedor:

- Heredada: `ApiKey`, `Model` (predeterminado `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — aún honrada como el
  proveedor predeterminado Anthropic.
- Multi-proveedor: `ActiveProvider` (tipo) y `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — importado a la tienda al inicio si aún no existen credenciales, para que un
  equipo de ops pueda enviar un despliegue configurado (incluyendo LLM local) puramente vía appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` sin cambios. Para pruebas/dev, una clave de configuración
vive en el [archivo de credenciales dev unificado](../testing/dev-credentials.md) bajo `Ai`.

## Confiabilidad

El proveedor se trata como no confiable — nada de lo que hace puede derribar la aplicación. Esto se mantiene idénticamente
para puntos finales en la nube y locales (un Ollama muerto reintentos luego se degrada exactamente como un Anthropic limitado):

- **Degradación elegante.** Cada modo de fallo (sin proveedor, HTTP 4xx/5xx/429, tiempo de espera, cuerpo malformado,
  contenido vacío, capacidad no soportada) devuelve un `AiResult.Fail(reason)` tipificado — el cliente nunca
  lanza a una página, herramienta MCP o servicio alojado.
- **Canalización de resiliencia.** `AddAiHttpClient` da el único cliente de IA compartido `HttpClient` un reintento limitado en
  fallas transitorias 5xx / red (retroceso exponencial + fluctuación) más tiempos de espera generosos por intento y totales
  (`AiHttp`), reutilizado por cada adaptador.

## Pruebas con el LLM local falso

La capa de IA se prueba de extremo a extremo **sin ninguna dependencia externa** por `FakeLocalLlmServer` — un pequeño
punto final **compatible con OpenAI** en proceso que devuelve una respuesta enlatada determinista, cable idéntica a
Ollama/LM Studio/vLLM. Respalda:

- **Unidad** — pruebas de traducción de solicitud por adaptador + análisis de respuesta, enrutamiento/degradación de capacidades.
- **Integración** — el adaptador compatible con OpenAI de extremo a extremo, la teoría de resiliencia parametrizada en
  cada adaptador y las **herramientas MCP de IA**.
- **E2E** — el `AiLocalFixture` arranca la aplicación apuntada al servidor falso (o un proveedor **real** cuando
  el desarrollador establece `AI_E2E_BASEURL` (+ opcional `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  ganan credenciales reales) e impulsa cada característica de IA a través de la interfaz de usuario real. Agregar o cambiar cualquier característica de IA
  **requiere** una prueba E2E a través de este dispositivo (ver el mandato de prueba del repositorio). Un carril opt-in
  (`AI_LOCAL_LLM=1`) ejecuta una completación real a través de un Contenedor de prueba **Ollama**.

## IA local integrada — cero configuración por defecto

El LLM local ONNX integrado funciona fuera de la caja: cuando su directorio de modelo está ausente y
`App:Ai:BuiltIn:AutoDownload` es `true` (el predeterminado), la aplicación descarga el modelo una vez en el
fondo desde `App:Ai:BuiltIn:DownloadBaseUrl`. Mientras se ejecuta la descarga, las llamadas de IA (y **Probar conexión** en Configuración → IA) devuelven un claro "el modelo se está descargando (configuración de primera vez)" mensaje
en lugar de un fallo duro. Los despliegues aislados de aire/medidos establecen `AutoDownload=false` y
pre-provisionan el directorio del modelo (`App:Ai:BuiltIn:ModelPath`). La puerta de etiqueta blanca
`App:Branding:AllowBuiltInAi` todavía se aplica.

La descarga también es **pre-calentada al inicio** cuando el modelo integrado es el proveedor activo, por lo que está
lista antes del primer clic de IA en lugar de fallar ese clic con "descargando…". **Configuración → IA**
expone el estado de instalación activo en la tarjeta del proveedor integrado — *Modelo listo* / *Descargando modelo…* /
*Modelo no instalado* / *Descarga fallida* — con un botón **Descargar modelo** (o **Reintentar descarga**) que inicia
la búsqueda de fondo única bajo demanda (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`).
Habilitar el proveedor integrado desde Configuración reutiliza la fila ya sembrada en lugar de agregar un duplicado,
por lo que nunca entra en conflicto con la restricción de un único proveedor activo.
