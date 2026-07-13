---
title: 0001 — DDD estricto con un Core puro
description: Por qué la lógica de dominio vive en agregados en un proyecto Core con cero dependencias de infraestructura.
---

# 0001 — DDD estricto con un `Core` puro

## Contexto

Esta aplicación mueve dinero real. Las reglas de negocio esparcidas en puntos finales, servicios de fondo y componentes Razor
se pudren en un comportamiento no comprobable e inconsistente — exactamente donde un error cuesta a un usuario capital.

## Decisión

La lógica de dominio vive **en agregados, objetos de valor y servicios de dominio** en `src/Core`, que
se compila con **cero dependencias de infraestructura** (sin EF, HttpClient, Docker o ASP.NET). Los puntos finales,
herramientas MCP, componentes y `BackgroundService`s **orquestan** — nunca deciden. Reglas:

- Sin setters públicos; cambios de estado a través de métodos que revelan la intención que protegen invariantes.
- Los agregados se hacen referencia entre sí por **ID fuerte**, nunca propiedad de navegación.
- Una `SaveChanges` muta **un** agregado; los flujos entre agregados utilizan eventos de dominio.
- Los primitivos que cruzan un límite de dominio se envuelven en objetos de valor.
- Las violaciones invariantes lanzan una `DomainException` de Core, no una excepción de framework.

## Consecuencias

- Las reglas de dominio son comprobables por unidad sin una base de datos o un host web.
- La pureza de `Core` es aplicada por máquina por `ArchitectureGuardTests` y fallaría la compilación si se rompiera.
- Hay más ceremonia (objetos de valor, IDs fuertes, eventos de dominio) que un modelo anémico — este es
  el costo deliberado de mantener las reglas de movimiento de dinero correctas y en un solo lugar.
