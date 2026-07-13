---
title: 0002 — El estado de la instancia es TPH; una transición reemplaza la entidad
description: Por qué el id de una instancia cambia a medida que se mueve a través de su ciclo de vida, y por qué el id del contenedor es la clave estable.
---

# 0002 — El estado de la instancia es TPH; una transición reemplaza la entidad

## Contexto

Una instancia de ejecución/backtest se mueve a través de estados (pendiente → programado → iniciando → ejecutando → terminal).
Modelamos el estado con **Table-Per-Hierarchy (TPH)** de EF Core: cada estado es un subtipo
(`StartingRunInstance`, `RunningRunInstance`, …). La columna discriminadora de TPH de EF **no puede cambiar** en
una fila existente.

## Decisión

Una transición de estado **reemplaza la entidad** con una nueva instancia de subtipo en lugar de mutar un campo de estado.
Porque la fila se reemplaza, el **id de la instancia cambia** a través de iniciando → ejecutando → terminal.
El **id del contenedor es estable** y se lleva a través de transiciones; el agente de nodo HTTP se indexa por
id de contenedor para estado/informe/parada/registros.

## Consecuencias

- Cada estado es un tipo distinto con solo los campos y métodos válidos en ese estado — transiciones ilegales
  y acceso a campos sin sentido son errores de compilación, no comprobaciones en tiempo de ejecución.
- Los llamadores **no deben** cachear un id de instancia a través de una transición; usa el id del contenedor como el identificador estable
  para cualquier cosa que abarque estados.
- La lógica de transición vive en `InstanceTransitions`; el cambio de id es intencional, no un error.
