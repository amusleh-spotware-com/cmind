---
description: "Contrarian Retail Positioning — transforma a porcentagem de traders de varejo longos em um vies contrario (faca o contrario da multidão quando e assimetrica), mais objetos de valor de sinal ponto-no-tempo que protegem contra visao de futuro."
---

# Posicionamento Contrariano de Varejo

A multidão de varejo e um dos poucos sinais de sentimento genuinamente uteis em FX — como um **indicador
contrario**. Quando a grande maioria dos traders de varejo estao longos, o preco historicamente tendeu a cair,
e vice-versa. Esta ferramenta transforma o posicionamento da multidão em uma leitura acionavel.

Abra **cBots → Contrarian Positioning** (`/quant/positioning`).

## O que faz

Insira a **% de traders de varejo longos** (da pagina de sentimento do seu corretor ou um feed como FXSSI) e
ele retorna:

- **Vies contrario** — **Bearish** quando >= 60% estao longos (multidao muito longa), **Bullish** quando <= 40% estao
  longos (multidao muito curta), **Neutral** na banda de indecisao 40-60%;
- **Forca** — quao assimetrica e a multidao (0 = balanceada, 1 = completamente de um lado), para ponderar o sinal.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Ponto-no-tempo por construcao

Por baixo, a camada de sinal (`Core.Signals`) modela um `PointInTimeSignal` que e **carimbado com o
momento em que era conhecivel** e recusa ser construido sem ele. Qualquer backtest ou agente autonomo que
consome um sinal verifica `IsKnownAt(decisionTime)` — entao dados futuros nunca podem vazar para uma decisao
historica. Visao de futuro e o maior matador de reprodutibilidade em financas quant; o modelo de dominio o
torna estruturalmente impossivel.

## Por que e confiavel

Codigo de dominio deterministico puro sem dependencia de infraestrutura — os limiares contrarios e o
guarda de ponto-no-tempo sao testados em unidade, incluindo as fronteiras 40/60 e rejeicoes fora de intervalo.
