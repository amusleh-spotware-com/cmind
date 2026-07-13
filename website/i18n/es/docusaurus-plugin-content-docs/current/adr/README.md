---
title: Registros de decisiones arquitectónicas
description: Las decisiones de diseño no obvias detrás de cMind — contexto, decisión y consecuencias — que no puedes leer en el código.
---

# Registros de decisiones arquitectónicas

Estos registran las decisiones de diseño que **no puedes inferir del código** — los compromisos, los caminos no
tomados y por qué. Cada uno es corto: *Contexto → Decisión → Consecuencias*. Decisión estructural nueva → agrega un
ADR aquí (siguiente número) para que el siguiente ingeniero (humano o IA) herede el razonamiento, no solo el
resultado.

| # | Decisión |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | DDD estricto con un `Core` puro |
| [0002](./0002-tph-instance-replaces-entity.md) | El estado de la instancia es TPH; una transición reemplaza la entidad |
| [0003](./0003-external-nodes-http-jwt.md) | Los nodos CLI de cTrader son HTTP + JWT, sin SSH/shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` se ejecuta en el host web en un contenedor de caja de arena |
| [0005](./0005-anthropic-raw-http.md) | El cliente de IA utiliza HTTP puro, no el SDK de Anthropic |
| [0006](./0006-copy-profile-db-lease.md) | El alojamiento de copia está coordinado por un arrendamiento de BD atómico |
