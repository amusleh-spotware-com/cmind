---
description: "Comisiones de rendimiento del gestor de dinero en una marca de agua alta, el modelo estándar de copy-trading (cTrader Copy, Darwinex, ZuluTrade profit-share): un proveedor cobra…"
---

# Copy performance fees (Fase 4)

**Comisiones de rendimiento del gestor de dinero** en una marca de agua alta, el modelo estándar de copy-trading (cTrader Copy, Darwinex, ZuluTrade profit-share): un proveedor cobra un porcentaje de *ganancias nuevas* por encima del pico de capital de cada seguidor — nunca en el saldo de apertura, y nunca dos veces por terreno ya recuperado. **Opt-in** vía `App:Copy:FeesEnabled` (desactivado por defecto).

## El modelo (marca de agua alta)

Por destino (cuenta de seguidor), cada liquidación:

1. **Primera liquidación** siembra la marca de agua alta (HWM) en el capital actual → sin cargo (un seguidor nunca se le cobra en su depósito).
2. **Nuevo máximo** (capital > HWM): `fee = performanceFeePercent × (capital − HWM)`, luego `HWM ← capital`.
3. **En o por debajo del pico**: sin cargo, HWM sin cambios — el seguidor debe primero recuperarse más allá del pico anterior, por lo que nunca se le cobra dos veces por las mismas ganancias.

La aritmética de comisiones es un invariante de dominio en `CopyDestination.SettleFee(equity)` — el agregado lo posee; el servicio de liquidación solo suministra el capital encuestado y registra la cantidad devuelta. `PerformanceFee` es un objeto de valor limitado al 50% para que una mala configuración no pueda cobrar la ganancia completa de un seguidor.

## Cómo se liquida

```
CopyFeeSettlementService (BackgroundService, solo cuando FeesEnabled)
   │  cada App:Copy:FeeSettlementInterval
   ├─ cargar perfiles en ejecución con destino configurado de tarifa
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader abre una sesión,
   │                                               calcula balance + P&L flotante (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← lógica HWM en el agregado
   └─ persistir HWM avanzado + agregar CopyFeeAccrual (solo en un nuevo máximo)
```

- `ICopyEquityReader` es una abstracción de Core; la implementación en vivo (`OpenApiCopyEquityReader`) es la única pieza de infra — por lo que la liquidación + lógica HWM se ejercita en pruebas con un lector falso, sin corredor en vivo.
- `CopyFeeAccrual` es un registro append-only (HWM-antes, capital, porcentaje de tarifa, cantidad de tarifa, liquidado-en) — un registro de hechos para el reporte de tarifa y facturación, no un agregado.

## Configuración y API

| configuración `App:Copy` | Defecto | Efecto |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Ejecutar el servicio de liquidación. |
| `FeeSettlementInterval` | `1h` | Con qué frecuencia se encuesta el capital y se liquidan las comisiones. |

Por destino: `PerformanceFeePercent` (0–50) se establece en el destino (solicitud de agregar/editar destino).

- `GET /api/copy/profiles/{id}/fees` — acumulaciones de tarifa del perfil + total cobrado.

## Pruebas

- **Unit** (`CopyPerformanceFeeTests`) — el invariante HWM: la primera liquidación siembra + no cobra nada; un nuevo máximo cobra solo la ganancia por encima del pico; en/debajo del pico no cobra nada y el pico nunca retrocede; después de un drawdown solo la recuperación más allá del pico anterior se cobra; 0% nunca cobra; el VO rechaza porcentajes fuera de rango.
- **Integration** (`CopyFeeSettlementTests`, Postgres real, lector de capital falso) — semilla→10k (sin cargo, marca sembrada), 12k (cobra 400, marca avanza), 11k (sin cargo, marca mantenida); acumulación persistida con propietario/cantidad correcto.

El host de copia no se toca por comisiones (la liquidación es un trabajo DB separado), por lo que la suite de estrés DST de copia no se ve afectada (23/23).
