---
title: Registros de Decisão Arquitetural
description: As decisões de design não óbvias por trás do cMind — contexto, decisão e consequências — que você não pode ler do código.
---

# Registros de Decisão Arquitetural

Estes registram as decisões de design que você **não pode inferir do código** — os trade-offs, os caminhos não tomados e o porquê. Cada um é curto: *Contexto → Decisão → Consequências*. Nova decisão estrutural → adicione um ADR aqui (próximo número) para que o próximo engenheiro (humano ou IA) herde o raciocínio, não apenas o resultado.

| # | Decisão |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | Strict DDD com um `Core` puro |
| [0002](./0002-tph-instance-replaces-entity.md) | O estado de instância é TPH; uma transição substitui a entidade |
| [0003](./0003-external-nodes-http-jwt.md) | Nós cTrader CLI são HTTP + JWT, sem SSH/shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` roda no host web em um container de sandbox |
| [0005](./0005-anthropic-raw-http.md) | O cliente AI usa HTTP bruto, não o SDK Anthropic |
| [0006](./0006-copy-profile-db-lease.md) | Hospedagem de cópia é coordenada por um lease atômico de DB |
