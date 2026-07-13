---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = in-memory IOpenApiTradingSession que todos os testes de cópia unitária executam. Trabalho: imitar real cTrader Open…"
---

# FakeTradingSession — contrato de fidelidade Open API cTrader

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = `IOpenApiTradingSession` em memória que todos os testes de cópia de unidade executam. Trabalho: imitar **servidor cTrader Open API real** perto o suficiente que testes de unidade cobrem comportamento que apenas camada ao vivo costumava capturar. Este doc = contrato de fidelidade: que fake modela, como fielmente, e regra o mantendo honesto.

> **Regra vinculante (CLAUDE.md):** fake permanece fiel a cTrader. **Estenda-o, nunca o enfraquça** para passar teste. Cada novo comportamento real que você confia é modelado aqui, preso por teste de fidelidade.

## Matriz de fidelidade (F1–F13)

Rastreia plano `plans/copy-trading-overhaul.md` §7.6. Legenda: ✅ modelado · ◑ parcial (opt-in / extensão) · ⬜ ainda não modelado.

| # | Comportamento real de Open API | Status fake | Como é modelado |
|---|------------------------|-------------|-------------------|
| F1 | Pedido de mercado pode **preenchimento parcial** | ◑ | `PartialFillFractionForCtid[ctid] = f` preenche apenas `f×volume`; reconciliar então mostra G5 verdadeiro (Fase-1). Par Accept→fill de evento ainda para vir. |
| F2 | Volume normalizado para **passo**, rejeitado abaixo de **min** / acima de **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` arredonda para baixo para passo, lança `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **SL/TP inválido** rejeitado (lado + dígitos) | ⬜ | Fase 0a/1 planeada (emparelha com M6 normalização de precisão SL/TP). |
| F4 | Preços **inteiro-escalados por dígitos**; `pipPosition` | ◑ | `SymbolDetails` agora carrega `Digits` (e `MaxVolume`), populado a partir de símbolo real; `PipPosition` direciona tolerância de faixa de mercado, `Digits` direciona normalização de precisão SL/TP (M6). Escala de preço inteiro completo ainda pendente. |
| F5 | **Faixa de mercado** preenche apenas se ponto ao vivo dentro de `base ± slippage`, caso contrário rejeita | ✅ | `IsMarketRangeRejected` compara ponto ao vivo (`SetSpot`) para `baseSlippagePrice ± slippageInPoints`. Sinalização legada `RejectMarketRangeForCtid` ainda força rejeição. |
| F6 | **Pendente disparo→preenchimento** evento duplo (Pedido carrega `positionId` + OPEN Posição) | ◑ | `PushOpen(..., orderId:)` reproduz evento pendente preenchido; FX‑Blue/cMAM cópia dupla dedupe coberta em `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Fechamentos acionados pelo servidor** (SL/TP bater, parada-out) | ⬜ | Hoje fecha teste-empurrado (`PushClose`); preço-acionado SL/TP-bater + parada-out fecha planeado. |
| F8 | **Por conta** tabelas de símbolo / detalhes | ◑ | Nomes/ids de símbolo por-fake; tabelas divergentes por-conta (cross-broker) pendentes. |
| F9 | Completo **estado de conta** (saldo, equity, margem, margemGrátis) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entrada/swap/comissão via `SetPositionValuation`) + `SetSpot` feed equity real em dimensionamento de equity proporcional (G2, teste de unidade em `CopyEquitySizingTests`). Margem usada não exposta por API de reconciliação, então margem livre relatada como equity. |
| F10 | Eventos carregam **carimbos de servidor** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — sessão real lê a partir de `ExecutionTimestamp` do deal; `PushOpen`/`PushPending` aceitam `serverTimestamp:` para que teste impulsionado `FakeTimeProvider` dirija latência de cópia real (G1). |
| F11 | **Modo de negociação / cronograma** (desabilitado / apenas fechamento / fechado) | ⬜ | Fase 2b planeada. |
| F12 | **Taxonomia de erro tipado** (`ProtoOAErrorRes` códigos) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` lança uma vez `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Invalidação de token** — token obsoleto → erro de autenticação | ✅ | `InvalidateToken(ctid)` marca token anexado obsoleto; chamadas de negociação lançam **real** `OpenApiException` com `OpenApiErrorKind.TokenInvalid` (código `CH_ACCESS_TOKEN_INVALID`), exatamente como servidor ao vivo, até `SwapAccessTokenAsync` instalar token novo. Alimenta teste M1 robustez de token. |

Os testes de fidelidade vivem em `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, padrões preservam comportamento legado

Cada botão de fidelidade **desligado por padrão** para que fake mantenha comportamento simples sempre-preenchimento para testes que não se importam. Teste opta em por conta:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (uma-vez)
session.InvalidateToken(slave);                                             // F13
```

## Caracterização + conformidade (planeado, mantém fake ≡ real)

Dois mecanismos mantêm fake honesto contra servidor real em movimento (rastreado, desembarcando Fase 0a):

1. **Caracterização ao vivo** (`LiveApiCharacterization`, contas demo, secrets-gated, `Inconclusive` em mercado fechado): conduzir Open API real, registrar verdade exata de arame (sequências de eventos, escalonamento, códigos de rejeição) em fixtures ouro verificadas no projeto de teste. Sem segredos em fixtures — apenas formas observadas.
2. **Harness de conformidade**: execute *mesmo* conjunto de cenário duas vezes — uma vez contra `FakeTradingSession`, uma vez contra sessão ao vivo (quando segredos presentes) — afirme resultados observáveis idênticos. Mudanças de servidor real → perna ao vivo falha → fake de atualização. Isto torna "testes de unidade cobrem tudo" confiável.

Credenciais ao vivo: `secrets/dev-credentials.local.json` (ou arquivos divididos legados) — veja `docs/testing/dev-credentials.md`.
