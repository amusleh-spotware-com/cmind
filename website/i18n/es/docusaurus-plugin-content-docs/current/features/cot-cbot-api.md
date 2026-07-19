# API de cBot COT

Los datos de Compromiso de Operadores se exponen a cBots y clientes externos sobre una API REST autenticada, de modo que una estrategia puede extraer posicionamiento (posición neta, % de interés abierto, índice COT) como entrada de señal. Reutiliza la **misma maquinaria JWT y alcance `market:read`** que la API de mercado de fortaleza de moneda — un token, un esquema.

## Autenticación

1. En la aplicación, emita un cliente de API de datos de mercado (propietario) y otórguele el alcance **`market:read`**.
2. Intercambie la id/secreto del cliente por un token portador de corta duración:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   La respuesta contiene `token`, `expiresAt` y los `scopes` otorgados.
3. Envíe el token en cada llamada COT:

   ```http
   Authorization: Bearer <token>
   ```

Un token faltante/inválido devuelve `401`; un token sin `market:read` devuelve `403`.

## Puntos finales

Ruta base `/api/market/v1/cot`. Todas las respuestas son JSON.

| Método y ruta | Propósito |
|---------------|---------|
| `GET /markets` | El catálogo de mercados-contratos rastreados. `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) y `q` de palabra clave opcionales. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | La instantánea semanal más reciente para un mercado. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Historial semanal en una ventana. |

Parámetros:

- `code` — el código de mercado de contrato CFTC (p. ej. `099741` para Euro FX; obténgalo de `/markets`).
- `kind` — `Legacy` (predeterminado), `Disaggregated` o `Tff`.
- `combined` — `true` para futuros + opciones, `false` (predeterminado) para solo futuros.
- `asOf` (ISO-8601, opcional) — ancla punto en el tiempo: solo se devuelven informes públicos en ese instante, por lo que un backtest no ve adelanto.

### Ejemplo

```http
GET /api/market/v1/cot/latest?code=088691&kind=Legacy HTTP/1.1
Authorization: Bearer <token>
```

```json
{
  "contractCode": "088691",
  "marketName": "Gold",
  "kind": "Legacy",
  "combined": false,
  "reportDate": "2024-01-02T00:00:00+00:00",
  "knownAt": "2024-01-05T20:30:00+00:00",
  "openInterest": 450000,
  "cotIndex": 82.4,
  "extreme": "LongExtreme",
  "categories": [
    { "category": "NonCommercial", "long": 250000, "short": 90000, "net": 160000, "longPercentOfOi": 55.5 }
  ]
}
```

## Herramientas MCP

El mismo modelo de lectura está disponible para clientes de IA como herramientas MCP: `CotMarkets`, `CotLatest`, `CotHistory` y `CotHealth` — cada una correcta punto en el tiempo a través de un `asOf` opcional. Consulte la [característica Compromiso de Operadores](./cot-report.md) para la imagen completa.

## Cierre

La API está detrás de la misma puerta de dos niveles que la página: `App:Branding:EnableCot` y `App:Features:Cot`. Con cualquiera desactivado, cada ruta bajo `/api/market/v1/cot` devuelve `404`.
