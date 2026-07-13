---
description: "Regime Lab — rotula uma serie de retornos em regimes de volatilidade Calma / Normal / Turbulenta e reporta performance por-regime, mais o expoente de Hurst (persistência de tendencia vs reversao a media). Determinístico."
---

# Laboratorio de Regimes

Um unico Sharpe ratio oculta a verdade de que a maioria das vantagens sao condicionais: otimas em mercados calmos e de tendencia
e mortas em turbulencia (ou o contrario). O Laboratorio de Regimes quebra o historico de uma estrategia em regimes de volatilidade
e mostra como ela performou em cada — entao voce sabe *quando* sua vantagem realmente funciona.

Abra **cBots → Regime Lab** (`/quant/regimes`).

## O que faz

Dado uma serie de retornos (ou curva de equity, mais antigo primeiro), ele:

- computa uma **volatilidade realizada trailing** em cada ponto e divide o historico em regimes **Calm**,
  **Normal** e **Turbulent** pelos tercis daquela volatilidade;
- reporta **performance por-regime** — observacoes, retorno medio, volatilidade e Sharpe — entao voce pode ver
  onde a vantagem vive;
- estima o **expoente de Hurst** via analise rescaled-range (R/S): acima de ~0.55 a serie e
  **com tendencia / persistente**, abaixo de ~0.45 e **reversora a media**, e ao redor de 0.5 e proxima de um
  random walk.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // or { "equity": [...] }
```

## Por que e confiavel

Codigo de dominio puro e deterministico (`Core.Regimes`) sem dependencia de infraestrutura e sem chamadas externas
— testado em unidade para separacao de regime (calm vs volatilidade turbulenta) e para a direcao de Hurst
(serie anti-persistente pontua abaixo de 0.5, uma tendencia persistente pontua acima). O mesmo sinal de regime alimenta
o loop de reflexao dos agentes autonomos, entao um agente pode se inclinar para os regimes onde sua vantagem e real.
