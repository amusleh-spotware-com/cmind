---
description: "Directorio navegable de estrategias de copia. El proveedor publica perfil de copia como listado con distintivo verificado-en vivo (cuenta fuente de estrategia opera dinero real, no…"
---

# Mercado de proveedores de copia (Fase 4)

Directorio navegable de estrategias de copia. El proveedor **publica** perfil de copia como listado con distintivo **verificado-en vivo** (cuenta fuente de estrategia opera dinero real, no demostración) más comisión de rendimiento. Seguidores navegan mercado, clasificados por puntuación de rendimiento proyectada desde datos de transparencia de ejecución.

## Modelo

- `CopyProviderListing` = agregado: `UserId`, `ProfileId`, nombre de visualización, descripción, comisión de rendimiento, `VerifiedLive`, `Published` + `PublishedAt`. Un listado por perfil (índice único).
- **Verificado-en vivo** derivado en tiempo de publicación desde fuente del perfil `TradingAccount.IsLive` — proveedor no puede auto-afirmar.
- Estadísticas de rendimiento **no almacenadas en listado** — proyección de modelo de lectura sobre registro de transparencia `CopyExecution` (tasa de relleno, latencia promedio, deslizamiento realizado promedio), por lo que mercado siempre refleja calidad de ejecución en vivo.

## Clasificación

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → puntuación 0–100: tasa de relleno domina (×60), baja latencia + bajo deslizamiento añaden (×20 cada), distintivo verificado-en vivo añade pequeño bonus de confianza. Determinístico + monótono, por lo que ordenamiento estable.

## API

- `POST /api/copy/profiles/{id}/publish` — publicar/actualizar listado de perfil (`DisplayName`, `Description`, `PerformanceFeePercent`); verificado-en vivo establecido desde cuenta fuente.
- `DELETE /api/copy/profiles/{id}/publish` — despu blicar.
- `GET /api/copy/marketplace` — todos los listados publicados, clasificados, cada uno con resumen de rendimiento (ejecuciones, tasa de relleno, latencia promedio, deslizamiento promedio, puntuación) + distintivo verificado-en vivo.

## Pruebas

- **Unidad** (`CopyProviderListingTests`) — invariantes de agregado: nombre de visualización requerido; publicar establece marca de tiempo; despu publicar oculta; actualizar reemplaza campos de visualización + comisión + distintivo.
- **Integración** (`CopyMarketplaceTests`, Postgres real) — listado publicado persiste con distintivo; un listado por perfil (índice único); puntuación de clasificación prefiere proveedores verificados/alto-relleno.

Host de copia sin afectar (listados + modelo de lectura solo), por lo que suite de estrés DST de copia sin afectar.
