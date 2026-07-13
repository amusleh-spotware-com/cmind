---
description: "Diario de Trading e Coach — analisa suas propias execucoes e backtests em busca de vazamentos comportamentais (sobre-concentracao, falhas repetidas, vies perdedor) e coacha sobre a estrategia que voce ja tem. Determinístico, com narrativa AI opcional."
---

# Diario de Trading e Coach

A categoria mais nova genuinamente util de AI-para-trading nao e prever o mercado — e analisar
*seu proprio* comportamento. O Diario de Trading transforma seu historico de execucoes e backtests em feedback honesto para
que voce possa melhorar a estrategia que voce ja tem.

Abra **AI → Trading Journal** (`/journal`).

## O que surface

De suas instancias (execucoes e backtests) ele computa, deterministicamente:

- **Contagens de win/loss/falha e taxa de acerto** em seus backtests;
- **Insights comportamentais** — os vazamentos que silenciosamente custam aos traders de varejo:
  - **Sobre-concentracao** — a maioria da sua atividade e em um simbolo;
  - **Falhas repetidas** — uma alta parcela de execucoes falharam ao construir ou configurar;
  - **Vies perdedor** — mais backtests perdendo do que ganhando (com uma nudge para rodar o Laboratorio de Integridade e
    verificar se a vantagem e real);
  - um atestado de saude quando nenhum dos anteriores se aplica.

```http
GET /api/journal
```

## Por que e confiavel

A analise comportamental e codigo de dominio puro e deterministico (`Core.Journal`) sem dependencia de infraestrutura
— testada em unidade para sobre-concentracao, falhas repetidas, vies perdedor, o caso balanceado e a conta vazia.
Os fatos veem primeiro; o coach AI (Portfolio Digest) e uma camada de narrativa opcional
por cima, controlada pela chave da API Anthropic, entao o diario funciona completamente sem AI configurada.
