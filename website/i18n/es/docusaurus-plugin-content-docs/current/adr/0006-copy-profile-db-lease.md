---
title: 0006 — El alojamiento de copia está coordinado por un arrendamiento de BD atómico
description: Por qué los perfiles de copia se reclaman a través de un arrendamiento atómico de Postgres en lugar de un coordinador dedicado, y cómo eso previene la copia doble.
---

# 0006 — El alojamiento de copia está coordinado por un arrendamiento de BD atómico

## Contexto

Un perfil de copia en ejecución debe ser alojado por **exactamente un** nodo — dos anfitriones en el mismo perfil significa
cada transacción de fuente se refleja dos veces (dinero real perdido). Los nodos van y vienen (escalado, bloqueos, actualizaciones graduales),
y no queremos un servicio coordinador separado para ejecutar y mantener vivo.

## Decisión

Cada `CopyEngineSupervisor` reclama perfiles con un **arrendamiento de BD atómico** en la tabla `CopyProfiles`:

- **Reclamar** — un `ExecuteUpdate` atómico (o `FOR UPDATE SKIP LOCKED` al limitar por nodo) toma
  perfiles que no están asignados *o* cuyo arrendamiento ha expirado. La atomicidad significa dos supervisores en carrera
  nunca reclaman la misma fila.
- **Renovar** — un nodo en vivo actualiza su arrendamiento cada ciclo, por lo que mantiene su reclamo.
- **Reclamar** — el arrendamiento de un nodo bloqueado expira, y un sobreviviente recoge el perfil en su próximo ciclo
  (auto-sanación). En apagado elegante, el nodo **libera** sus arrendamientos inmediatamente para que el failover sea rápido.
- **Vigilancia** — un anfitrión cuya tarea ha salido mientras el perfil sigue siendo nuestro se reinicia.
- La reconciliación es temblorosa para evitar una manada de `UPDATE`s ruidosa a escala.

## Consecuencias

- No hay coordinador independiente para desplegar o mantener saludable — Postgres es la fuente única de verdad.
- La copia doble se previene mediante atomicidad a nivel de fila, no por bloqueo a nivel de aplicación.
- La latencia de failover está limitada por el TTL de arrendamiento (menos la ruta rápida de lanzamiento elegante).
- Este es el camino del dinero; está protegido por el conjunto de estrés determinista (DST) — nunca debilites un escenario DST
  para hacerlo pasar.
