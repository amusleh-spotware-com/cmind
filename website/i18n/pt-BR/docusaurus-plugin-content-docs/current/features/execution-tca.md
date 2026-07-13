---
description: "Analise de Custo de Transacao — mede qualidade de execucao (slippage em basis points e implementation shortfall) de uma ordem contra seu preco de chegada, a vantagem de execucao composta que bancos vivem. Determinístico."
---

# Analise de Custo de Transacao (TCA)

Alpha de execucao e minimo por trade e enorme sobre milhares deles — e uma grande parte de como bancos
e desks prop mantem sua vantagem. TCA mede o quanto o preco que voce realmente conseguiu derivou do
preco quando voce *decidiu* negociar.

Abra **cBots → Execution Cost** (`/quant/tca`).

## O que mede

Dado o **preco de chegada (decisao)**, o **lado**, e seus **fills** (preco x quantidade), ele reporta:

- **Preco medio de fill (VWAP)** — o preco ponderado por volume que voce realmente obteve.
- **Slippage (bps)** — o desvio de chegada para VWAP em basis points, **com sinal para que um número positivo é um
  custo** (comprando acima da chegada ou vendendo abaixo) e um número negativo é melhoria de preço.
- **Implementation shortfall** — aquele custo expresso em termos de preco x quantidade: o dinheiro que o desvio custou
  nesta ordem.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Fatiamento inteligente (Almgren-Chriss)

Além de medir custo, cMind pode planejar uma ordem grande para *minimizar* ele. **cBots → Execution Schedule**
(`/quant/execution`) constroi uma **agenda de execução ótima Almgren-Chriss**: dada a quantidade total,
um número de fatias, sua aversão a risco, volatilidade e impacto de mercado temporário, ele retorna o tamanho para
negociar em cada fatia. Aversão a risco mais alta **frontaliza** a agenda (cortando risco de timing); aversão zero a risco achata para um TWAP uniforme. As fatias sempre somam ao total.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Por que e confiavel

Codigo de dominio deterministico puro (`Core.Execution`) sem dependencia de infraestrutura e sem chamadas externas
— testado em unidade para o sinal de custo compra/venda, melhoria de preço, slippage zero, agregação VWAP, e
guardas de input. Esta e a metade de medicao de qualidade de execucao; e a mesma metrica de shortfall que o motor de copia
usa para julgar (e, com fatiamento inteligente, reduzir) o custo de ordens espelhadas.
