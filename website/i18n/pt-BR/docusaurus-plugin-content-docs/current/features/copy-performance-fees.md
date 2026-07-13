---
description: "Taxas de performance de gestor sobre alta-marca-dagua, o modelo padrao de copy-trading (cTrader Copy, Darwinex, ZuluTrade profit-share): um provedor cobra…"
---

# Taxas de performance de copia (Fase 4)

**Taxas de performance de gestor sobre alta-marca-dagua**, o modelo padrao de copy-trading (cTrader Copy,
Darwinex, ZuluTrade profit-share): um provedor cobra um percentual do *novo* lucro acima do
patrimonio de pico de cada seguidor — nunca sobre o saldo de abertura, e nunca duas vezes pelo mesmo ganho. **Opt-in** via
`App:Copy:FeesEnabled` (desligado por padrao).

## O modelo (alta-marca-dagua)

Por destino (conta seguidora), cada liquidacao:

1. **Primeira liquidacao** semeia a alta-marca-dagua (HWM) no patrimonio atual → sem cobranca (um seguidor nunca
   e cobrado sobre seu deposito).
2. **Novo maximo** (patrimonio > HWM): `fee = performanceFeePercent × (patrimonio − HWM)`, entao `HWM ← patrimonio`.
3. **No ou abaixo do pico**: sem taxa, HWM inalterada — o seguidor deve primeiro recuperar acima do velho pico, entao
   nunca e cobrado duas vezes pelos mesmos ganhos.

A aritmetica de taxa e um invariante de dominio em `CopyDestination.SettleFee(equity)` — o agregado a possui; o
servico de liquidacao apenas fornece o patrimonio consultado e registra o valor retornado. `PerformanceFee` e um
objeto de valor limitado a 50%, entao uma configuracao errada nao pode cobrar todo o ganho do seguidor.

## Como liquida

```
CopyFeeSettlementService (BackgroundService, somente quando FeesEnabled)
   │  a cada App:Copy:FeeSettlementInterval
   ├─ carrega perfis em execucao com destino configurado para taxa
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader abre sessao,
   │                                               computa saldo + P&L flutuante (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← logica HWM no agregado
   └─ persiste HWM avanzada + append CopyFeeAccrual (somente em novo maximo)
```

- `ICopyEquityReader` e uma abstração Core; a implementacao real (`OpenApiCopyEquityReader`) e a unica
  peca de infra — entao a logica de liquidacao + HWM e exercitada em testes com um reader fake, sem corretora live.
- `CopyFeeAccrual` e um log append-only (HWM-antes, patrimonio, fee %, valor da taxa, liquidado-em) — um log de
  fatos para o relatorio de taxas e cobranca, nao um agregado.

## Configuracao e API

| Configuracao `App:Copy` | Padrao | Efeito |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Executa o servico de liquidacao. |
| `FeeSettlementInterval` | `1h` | Com que frequencia patrimonio e consultado e taxas liquidadas. |

Por destino: `PerformanceFeePercent` (0-50) e definida no destino (requisicao de adicionar/editar destino).

- `GET /api/copy/profiles/{id}/fees` — accruals de taxa do perfil + total cobrado.

## Testes

- **Unidade** (`CopyPerformanceFeeTests`) — o invariante HWM: primeira liquidacao semeia + nao cobra nada; um
  novo maximo cobra somente o ganho acima do pico; no/abaixo do pico nao cobra e o pico nunca recua;
  apos drawdown somente a recuperacao acima do velho pico e cobrada; 0% nunca cobra; o VO rejeita
  percentuais fora do intervalo.
- **Integracao** (`CopyFeeSettlementTests`, Postgres real, fake equity reader) — seed→10k (sem cobranca, marca
  semeada), 12k (cobra 400, marca avanca), 11k (sem cobranca, marca mantida); accrual persistido com o
  dono/valor corretos.

O host de copia e intocado por taxas (liquidacao e um job DB separado), entao a suite DST de copia e
inalterada (23/23).
