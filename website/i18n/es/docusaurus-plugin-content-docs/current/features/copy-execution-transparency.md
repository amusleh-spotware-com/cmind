---
description: "Hechos de ejecución por copia — latencia, deslizamiento realizado, relleno vs fracaso — capturados en cada intento de copia, superficializados como reporte de transparencia por perfil. Desactivado por…"
---

# Copy execution transparency (Fase 3)

Hechos de ejecución por copia — latencia, deslizamiento realizado, relleno vs fracaso — capturados en cada intento de copia, superficializados como reporte de transparencia por perfil. **Desactivado por defecto**; habilitar con `App:Copy:TransparencyEnabled=true`. Cuando está desactivado, el motor de copia es byte-for-byte sin cambios: el host emite a un sumidero sin operación, nada se escribe.

## Cómo funciona

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparency off) NullCopyEventSink   → descarta (por defecto; cero costo de ruta caliente)
             (transparency on)  ChannelCopyEventSink → canal en memoria acotado (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  por lotes cada intervalo de drenaje de App
                                   ▼
                          Tabla append-only CopyExecution  ◀── GET /api/copy/profiles/{id}/transparency
```

- **La ruta caliente se mantiene libre de I/O.** El host llama a `ICopyEventSink.Record(...)` — encolado no bloqueante, nunca lanzando. Nunca espera, nunca toca DB, nunca bloquea ejecución de orden.
- **Pérdida preferida sobre contrapresión.** Canal acotado (`CopyExecutionChannelCapacity`) con `DropOldest`: si el drenador de DB se atasca, las filas de transparencia *más antiguas* se descartan en lugar de retrasar una copia. Transparencia = telemetría de mejor esfuerzo, no dependencia de operaciones.
- **Persistencia fuera de banda.** `CopyExecutionDrainer` drena el canal en lotes (`CopyExecutionDrainBatchSize`) en `CopyExecutionDrainInterval`, escribe filas de `CopyExecution` a través de `DataContext` con alcance. Descarga final en apagado.
- **Hechos, no comandos.** `CopyExecution` = registro append-only (como `InstanceLog`/`AuditLog`), no agregado. El modelo de lectura lo consulta directamente (CQRS-lite), agrega en memoria.

## Qué se registra

Un `CopyExecutionRecord` por intento de copia en un destino:

| Tipo | Cuándo | Lleva |
|------|--------|-------|
| `Opened` | orden de copia colocada | símbolo, lado, volumen de cable, precio maestro, deslizamiento realizado (puntos), latencia (ms) |
| `Failed` | apertura de copia lanzada/rechazada | símbolo, lado, volumen/precio maestro, latencia, motivo del fallo (tipo de excepción) |

(`Closed`/`Skipped`/`Reconciled` existen en enum para expansión futura.)

## El reporte

`GET /api/copy/profiles/{id}/transparency` (con alcance de propietario) retorna, sobre los 500 hechos más recientes:

- **Resumen** — total, abiertos, fallidos, **tasa de relleno**, **latencia promedio (ms)**, **deslizamiento promedio (puntos)**.
- **Recientes** — hechos brutos recientes (destino, posición de fuente, símbolo, lado, volumen, precio maestro, deslizamiento, latencia, motivo, marca de tiempo).

## Configuración (`App:Copy`)

| Configuración | Defecto | Efecto |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Activar captura de hecho por copia + drenador para el nodo. |

Capacidad de canal, tamaño de lote de drenaje, intervalo de drenaje = constantes de `CopyDefaults` (`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Pruebas

- **Unit** (`CopyTransparencyTests`) — apertura exitosa emite hecho `Opened` con símbolo/lado/volumen/latencia correcto; apertura rechazada emite hecho `Failed` con motivo. Impulsado a través de sumidero de captura.
- **Integration** (`CopyExecutionDrainerTests`, Postgres real) — drenador persiste hechos almacenados en búfer en registro `CopyExecution`; sumidero vacío no escribe nada.
- **DST** — cambio de host disparo y olvida con sumidero sin operación predeterminado, por lo que la suite de estrés de copia determinista permanece verde (23/23).
