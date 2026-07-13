---
description: "Feed por propietario de eventos de copia relevantes de seguridad — destino activando interruptor de rechazo, violación de protección de cuenta o regla de propiedad, aplanamiento de pánico. Activado por…"
---

# Copy operational notifications (Fase 2b)

Feed por propietario de eventos de copia relevantes de seguridad — destino activando interruptor de rechazo, violación de protección de cuenta o regla de propiedad, aplanamiento de pánico. **Activado por defecto** (`App:Copy:NotificationsEnabled`, por defecto `true`); establecer false para silenciar. Concepto propio en contexto de Copia, separado del agregado `AlertRule` de mercado/IA.

## Cómo funciona

Mismo patrón host→sink→drainer fuera de banda que el registro de transparencia de ejecución:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications off) NullCopyNotificationSink   → descarta (sin-op; motor sin cambios)
             (notifications on)  ChannelCopyNotificationSink → canal acotado DropOldest
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  resuelve propietario de cada perfil, por lotes
                                     ▼
                            Feed CopyNotification  ◀── GET /api/copy/notifications
```

- Host `Notify(...)` no bloqueante, nunca lanza — nunca toca DB, nunca retrasa copia.
- Drenador resuelve `UserId` propietario desde la notificación de perfil de cada uno; notificación cuyo perfil se fue (propietario irresuelto) descartada, no huérfana.
- `CopyNotification` = feed append-only, reconocible por fila (no agregado).

## Qué se genera

| Tipo | Severidad | Cuándo |
|------|-----------|--------|
| `DestinationTripped` | Advertencia | Presupuesto de rechazo G8 agotado; nuevas aperturas pausadas para el enfriamiento. |
| `AccountProtectionTriggered` | Crítico | Piso/techo de capital ZuluGuard violado; aperturas bloqueadas (SellOut liquida). |
| `PropRuleBreached` | Crítico | Pérdida diaria de propiedad / drawdown final violada; destino aplanado + bloqueado por el día. |
| `FlattenAll` | Crítico | Aplanamiento de pánico ejecutado; cada destino cerrado + bloqueado. |
| `TokenInvalidated` | (reservado) | El token de un destino fue invalidado; esperando rotación. |

## API

- `GET /api/copy/notifications` (con alcance de propietario) — notificaciones recientes del usuario (más recientes 200) en todos los perfiles, más conteo **no reconocido**.
- `POST /api/copy/notifications/{id}/acknowledge` — marcar uno como leído.

## Configuración (`App:Copy`)

| Configuración | Defecto | Efecto |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Emitir notificaciones de seguridad + ejecutar el drenador. `false` → sumidero sin-op. |

## Pruebas

- **Unit** (`CopyNotificationTests`) — destino activado genera `DestinationTripped`; aplanamiento de pánico genera `FlattenAll` a nivel de perfil. Vía sumidero de captura.
- **Integration** (`CopyNotificationDrainerTests`, Postgres real) — drenador resuelve propietario + persiste; notificación para perfil desconocido descartada.
- **DST** — host emite disparo y olvida con sumidero sin-op predeterminado, por lo que la suite de estrés de copia permanece verde (23/23).
