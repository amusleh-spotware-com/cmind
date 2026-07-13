---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = IOpenApiTradingSession en memoria que todas las pruebas de unidad de copia de trading se ejecutan contra. Trabajo: imitar real de Open API cTrader…"
---

# FakeTradingSession — Contrato de fidelidad de Open API cTrader

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = en memoria `IOpenApiTradingSession` todas las pruebas de unidad de copia de trading se ejecutan contra. Trabajo: imitar **servidor Open API cTrader real** lo suficientemente cerca que las pruebas de unidad cubran comportamiento solo que nivel en vivo solia atrapar. Este documento = contrato de fidelidad: qué modelos falsos, cuán fielmente, y regla manteniéndolo honesto.

> **Regla vinculante (CLAUDE.md):** falso permanece fiel a cTrader. **Extiéndelo, nunca lo debilites** para pasar prueba. Cada nuevo comportamiento real en el que confías se modela aquí, fijado por prueba de fidelidad.

## Matriz de fidelidad (F1–F13)

Rastrea plan `plans/copy-trading-overhaul.md` §7.6. Leyenda: ✅ modelado · ◑ parcial (optar por participar / extender) · ⬜ aún no modelado.

| # | Comportamiento Open API real | Estado falso | Cómo se modela |
|---|------------------------|-------------|-------------------|
| F1 | Orden de mercado puede **llenar parcialmente** | ◑ | `PartialFillFractionForCtid[ctid] = f` llena solo `f×volume`; reconciliar luego muestra cierre de brecha Fase‑1 verdadero‑arriba (G5). El par evento aceptación→relleno aún está por venir. |
| F2 | Volumen normalizado a **paso**, rechazado por debajo **mínimo** / por encima **máximo** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` redondea hacia abajo a paso, lanza `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **SL/TP inválido** rechazado (lado + dígitos) | ⬜ | Fase planeada 0a/1 (se empareja con normalización de precisión SL/TP M6). |
| F4 | Precios **escalados por enteros por dígitos**; `pipPosition` | ◑ | `SymbolDetails` ahora lleva `Digits` (y `MaxVolume`), poblado desde símbolo real; `PipPosition` impulsa tolerancia de rango de mercado, `Digits` impulsa normalización de precisión SL/TP (M6). Escalado de precio entero completo aún pendiente. |
| F5 | **Rango de mercado** llena solo si spot dentro de `base ± slippage`, sino rechaza | ✅ | `IsMarketRangeRejected` compara spot en vivo (`SetSpot`) a `baseSlippagePrice ± slippageInPoints`. Bandera `RejectMarketRangeForCtid` heredada aún fuerza rechazo. |
| F6 | **Desencadenante pendiente→relleno** evento dual (Orden lleva `positionId` + Posición ABIERTA) | ◑ | `PushOpen(..., orderId:)` reproduce evento de relleno-pendiente; deduplicación doble-copia FX‑Blue/cMAM cubierta en `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Cierres impulsados por servidor** (SL/TP golpe, parada) | ⬜ | Hoy cierra empujada por prueba (`PushClose`); cierres impulsadas por precio SL/TP-golpe + parada planeadas. |
| F8 | **Por cuenta** tablas de símbolos / detalles | ◑ | Nombres/ids de símbolo por-falso; tablas divergentes por-cuenta (entre brokers) pendiente. |
| F9 | **Estado de cuenta** completo (balance, equidad, margen, margenLibre) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entrada/swap/comisión vía `SetPositionValuation`) + `SetSpot` alimentan equidad real a dimensionamiento proporcional-equidad (G2, unit-probado en `CopyEquitySizingTests`). Margen usado no expuesto por API de reconciliación, entonces margen libre reportado como equidad. |
| F10 | Eventos llevan **marcas de tiempo del servidor** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — sesión real lee desde `ExecutionTimestamp` del acuerdo; `PushOpen`/`PushPending` aceptan `serverTimestamp:` entonces prueba impulsada por `FakeTimeProvider` impulsa latencia real de copia (G1). |
| F11 | **Modo de trading / horario** (deshabilitado / solo-cierre / cerrado) | ⬜ | Fase planeada 2b. |
| F12 | **Taxonomía de error tipada** (códigos `ProtoOAErrorRes`) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` lanza de un disparo `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Invalidación de token** — token antiguo → error de autenticación | ✅ | `InvalidateToken(ctid)` marca token adjunto antiguo; llamadas de trading lanzan **real** `OpenApiException` con `OpenApiErrorKind.TokenInvalid` (código `CH_ACCESS_TOKEN_INVALID`), exactamente como servidor en vivo, hasta `SwapAccessTokenAsync` instala token fresco. Alimenta prueba de robustez de token M1. |

Las pruebas de fidelidad viven en `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Optar por participar, los predeterminados preservan comportamiento heredado

Cada botón de fidelidad **desactivado de forma predeterminada** para que falso permanezca simple siempre-llenar comportamiento para pruebas que no importa. Prueba opta por participar por cuenta:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (de un disparo)
session.InvalidateToken(slave);                                             // F13
```

## Caracterización + conformidad (planeado, mantiene falso ≡ real)

Dos mecanismos mantienen falso honesto contra servidor real móvil (rastreado, aterrizando en Fase 0a):

1. **Caracterización en vivo** (`LiveApiCharacterization`, cuentas de demostración, secretos-controlados, `Inconclusive` en mercado cerrado): impulsar Open API real, grabar verdad de alambre exacta (secuencias de evento, escalado, códigos de rechazo) en accesorios dorados verificados en proyecto de prueba. Sin secretos en accesorios — solo formas observadas.
2. **Arnés de conformidad**: ejecutar *misma* suite de escenario dos veces — una vez contra `FakeTradingSession`, una vez contra sesión en vivo (cuando secretos presentes) — aseveran resultados observables idénticos. Cambios de servidor real → pierna en vivo falla → actualizar falso. Esto hace "pruebas de unidad cubren todo" confiable.

Credenciales en vivo: `secrets/dev-credentials.local.json` (o archivos de split heredados) — véase `docs/testing/dev-credentials.md`.
