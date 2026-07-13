---
description: "Strategy Health & Alpha Decay — deteccao deterministica de decadencia que compara o Sharpe recente de uma estrategia com seu registro anterior e localiza o maior shift de media (CUSUM change-point), retornando um veredicto Healthy / Degrading / Decayed."
---

# Saude da Estrategia e Decadencia de Alpha

Toda vantagem decai — a pesquisa e direta de que a vida media de uma estrategia quant caiu de anos
para meses, entao *adaptacao supera descoberta*. O monitor de Saude da Estrategia informa voce, a partir do proprio
historico de retornos de uma estrategia, se a vantagem ainda existe.

Abra **cBots → Strategy Health** (`/quant/health`).

## O que faz

Dado uma serie de retornos (ou curva de equity, mais antigo primeiro), ele:

- divide o historico em uma metade **anteriors** e **recente** e compara seus Sharpe ratios;
- roda um scan **CUSUM change-point** para localizar a observacao onde a media mais claramente mudou (uma
  quebra de regime), reportada somente quando o desvio e estatisticamente notavel;
- retorna um veredicto:

| Veredicto | Significado |
|---|---|
| **Healthy** | Performance recente esta em linha com (ou melhor que) o registro anterior. |
| **Degrading** | Sharpe recente e materialmente mais fraco que o registro anterior — monitore de perto. |
| **Decayed** | A vantagem desapareceu efetivamente na janela recente — considere pausar. |
| **Unknown** | Historico insuficiente para julgar. |

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Por que e confiavel

E codigo de dominio puro, deterministico (`Core.Health`) sem dependencia de infraestrutura e sem chamadas externas
— testado em unidade para os casos decayed, degrading, healthy e muito-curto e para a localizacao de change-point.
E o companheiro manual dos checques de saude always-on que apoiam os agentes autonomos:
as mesmas estatisticas guiam o disjuntor que reduz risco de uma estrategia live cuja vantagem esta desvanecendo.
