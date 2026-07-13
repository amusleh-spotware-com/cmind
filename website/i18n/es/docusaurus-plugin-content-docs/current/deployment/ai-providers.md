---
description: "Catálogo de configuración para cada proveedor de IA que cMind soporta — Anthropic, OpenAI, Azure OpenAI, Google Gemini y cada punto final compatible con OpenAI incluyendo modelos locales (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) y nubes compatibles con OpenAI."
---

# Proveedores de IA — catálogo de configuración

La capa de IA de cMind es agnóstica del proveedor (ver [características de IA](../features/ai.md)). Configura un proveedor de dos
formas:

1. **Interfaz de usuario (propietario):** Configuración → IA → **Agregar proveedor** → seleccionar tipo, URL base, modelo, clave (opcional para
   local), alternar capacidades, **Establecer activo** → **Prueba conexión**.
2. **Config/env (ops):** semilla `App:Ai:Providers[]` e `App:Ai:ActiveProvider` — importado a la tienda
   en el primer inicio cuando no existen credenciales. Ejemplo (env, índice de proveedor `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (omitir para puntos finales locales sin clave)
   ```

Exactamente un proveedor está activo a la vez. Las claves se almacenan encriptadas; un punto final local no necesita ninguno.

## Seguridad: http vs https

El texto sin formato `http://` se acepta **solo** para loopback / privado (intranet) anfitriones — el caso de LLM local
(Ollama, LM Studio, vLLM, un cuadro en las instalaciones). Cualquier anfitrión enrutable en Internet público **debe** ser
`https://`, para que una clave API nunca se envíe en texto claro. Aire-separado/en las instalaciones: apunta la URL base en tu
punto final interno (loopback o IP privada) y deja la clave en blanco si el tiempo de ejecución no está autenticado.

## IA local integrada (ONNX, enviada)

cMind envía un **LLM local en proceso real** (Microsoft.ML.OnnxRuntimeGenAI) que está **activado por
defecto** — sin clave, sin servicio externo. Al primer inicio, cuando no hay proveedor configurado y
`App:Branding:AllowBuiltInAi` es `true`, se siembra y activa automáticamente.

- **Config:** `App:Ai:BuiltIn:Enabled` (defecto `true`), `App:Ai:BuiltIn:ModelPath` (defecto
  `models/onnx`, relativo al directorio base de la aplicación), `App:Ai:BuiltIn:MaxTokens` (defecto `1024`).
- **Archivos de modelo:** apunta `ModelPath` a un directorio que contenga un modelo ONNX GenAI — `genai_config.json`,
  el tokenizador y los pesos `.onnx`. Una compilación CPU **Phi-3-mini** funciona bien, por ejemplo:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # luego establece App:Ai:BuiltIn:ModelPath a esa carpeta (contiene genai_config.json)
  ```

  Agrupa la carpeta con tu imagen de despliegue / volumen Helm, o móntala en tiempo de ejecución. Cuando los archivos no están
  presentes el integrado se degrada a un claro mensaje "modelo no instalado" — la aplicación aún se ejecuta; configura
  otro proveedor o instala el modelo.
- **GPU:** cambia el paquete/modelo de CPU por una compilación ONNX GenAI de CUDA/DirectML; la ruta de código es sin cambios.

## Etiqueta blanca: limitando IA

Establecer bajo `App:Branding` (aplicado del lado del servidor — un upsert prohibido devuelve `400`):

- `AllowBuiltInAi: false` — elimina completamente el modelo integrado enviado.
- `AllowLocalProviders: false` — prohíbe puntos finales locales/auto-alojados (Ollama/LM Studio/vLLM y cualquier
  URL de loopback/privada compatible con OpenAI).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — permitir solo estos tipos (vacío = todos).

## Extensión con futuros modelos integrados

La capa de proveedor se basa en adaptadores (`IAiProvider` con clave `AiProviderKind`), por lo que un tiempo de ejecución de modelo integrado futuro
se agrega sin tocar ninguna característica de IA: agrega un tipo, implementa un adaptador, regístralo. El
integrado ONNX es la implementación de referencia. Ver [Características de IA → Extensión](../features/ai.md#extending-future-built-in-models).

## Proveedores de nube

### Anthropic (Claude)

- Clave: <https://console.anthropic.com/> → Claves API.
- URL base: `https://api.anthropic.com/` · Modelo: ej. `claude-opus-4-8`.
- Capacidades: búsqueda web + visión activadas por defecto.

### OpenAI

- Clave: <https://platform.openai.com/api-keys>.
- URL base: `https://api.openai.com/v1/` · Modelo: ej. `gpt-4o`.
- Tipo: **OpenAiCompatible**. Habilita visión en el diálogo si usas un modelo de visión.

### Azure OpenAI

- Clave + punto final: Portal de Azure → tu recurso de Azure OpenAI.
- URL base: `https://<resource>.openai.azure.com/` · Modelo: tu **nombre de despliegue**.
- Tipo: **AzureOpenAi** (usa el encabezado `api-key` + consulta `api-version` y la ruta de despliegue).

### Google Gemini

- Clave: <https://aistudio.google.com/app/apikey>.
- URL base: `https://generativelanguage.googleapis.com/` · Modelo: ej. `gemini-2.0-flash`.
- Tipo: **Gemini**. Fundamento de búsqueda web + visión activados por defecto.

### Otras nubes compatibles con OpenAI (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Tipo: **OpenAiCompatible**. URL base = punto final compatible con OpenAI del proveedor, Modelo = su id de modelo,
  ApiKey = clave del proveedor. Sin cambio de cMind necesario — un adaptador los sirve a todos.

## Modelos locales (sin clave)

Todos los tiempos de ejecución locales exponen el cable Chat Completions de OpenAI, así que usa **Tipo: OpenAiCompatible** con la
URL base del tiempo de ejecución y nombre de modelo servido; deja la clave en blanco.

### Ollama

```
# instala desde https://ollama.com, luego:
ollama pull llama3.1:8b
```

- URL base: `http://localhost:11434/v1/` · Modelo: el nombre extraído (ej. `llama3.1:8b`, `qwen2.5-coder`).
- Sin clave API. Las capacidades predeterminadas son solo texto; habilita visión solo para un modelo de visión.

### LM Studio

- Inicia el servidor local (Desarrollador → Iniciar servidor).
- URL base: `http://localhost:1234/v1/` · Modelo: el id del modelo cargado. Sin clave API.
