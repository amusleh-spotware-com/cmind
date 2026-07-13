---
description: "Backtest Integrity Lab — estatisticas de overfitting deterministicas e de nivel institucional (Probabilistic & Deflated Sharpe, t-stat) que transformam um backtest bruto em veredicto Robusto / Fragil / Overfit, corrigindo para quantas configuracoes voce tentou."
---

# Laboratorio de Integridade de Backtest

Plataformas de varejo mostram o Sharpe ou lucro liquido de um backtest e param. Instituicoes nunca confiam em um
backtest bruto — elas perguntam se o resultado sobrevive **a correcao para visao de selecao e o numero de
configuracoes tentadas**. O Laboratorio de Integridade de Backtest traz essa verificacao ao cMind. E **matematica deterministica**
(sem AI, sem chamadas externas), entao o veredicto e reprodivel e cada numero e explicavel.

Abra em **cBots → Integrity** (`/quant/integrity`).

## O que computa

Dado uma serie de retornos (ou curva de equity/balanco) e o numero de conjuntos de parametros que voce tentou para chegar
neles, o analisador reporta:

- **Sharpe ratio** — por periodo e anualizado (raiz-quadrada-do-tempo).
- **Probabilistic Sharpe Ratio (PSR)** — a confianca de que o *verdadeiro* Sharpe bate o benchmark,
  accounting for track-record length, skewness e kurtosis (Bailey & Lopez de Prado, 2012). Um registro curto ou
  de cauda grossa o diminui.
- **Deflated Sharpe Ratio (DSR)** — PSR medido contra um **benchmark deflacionado**: o Sharpe que voce esperaria do
  *melhor de N trials aleatorios* sob o null (o False Strategy Theorem). Quanto mais
  configuracoes voce tentou, maior a barra — isso e o que pega overfitting.
- **t-statistic** da media dos retornos. Seguindo Harvey, Liu & Zhu, uma vantagem genuina deve limpar **t >= 3.0**,
  nao o 2.0 do livro texto.
- **Skewness / kurtosis** dos retornos, que alimentam as correcoes PSR/DSR.

## O veredicto

| Veredicto | Significado | Regra |
|---|---|---|
| **Robust** | A vantagem sobrevive aos trials que voce executou. | DSR >= 95% **and** PSR >= 95% **and** |t| >= 3.0 |
| **Fragile** | Estatisticamente vivo mas nao convincentemente — nao aumente de tamanho com base nisso sozinho. | entre os dois |
| **Overfit** | Muito provavelmente um artefato de visao de selecao, nao uma vantagem real. | DSR < 90% |

Cada resultado carrega uma justificativa em portugues simples para que o "por que" nunca esteja oculto.

## Probabilidade de Overfitting de Backtest (entre trials)

Alimentar uma contagem de trials e bom; alimentar a **serie real out-of-sample de cada configuracao que
voce tentou** e melhor. Cole no **grid de trials** opcional (uma serie por linha) e cMind executa
**Combinatorial Symmetric Cross-Validation** (Bailey, Borwein, Lopez de Prado & Zhu, 2015): ele divide
as observacoes em grupos, e para cada forma de escolher metade como in-sample ele escolhe a melhor
configuracao in-sample e verifica se aquele vencedor cai no fundo da metade **out-of-sample**.
A **Probabilidade de Overfitting de Backtest (PBO)** e a fracao de divisoes onde o vencedor falhou em
generalizar. Um PBO perto de 0 significa a melhor configuracao e genuinamente a melhor; um PBO de 0.5 ou mais significa seu
processo de selecao esta escolhendo ruido — o veredicto se torna **Overfit** independent do quao bom o vencedor parecia.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Quando o otimizador nativo do cTrader Console landar, cMind alimentara sua superficie de trial completa aqui
automaticamente.

## Trials — o numero que importa

`Trials` e **quantos conjuntos de parametros voce testou** antes de escolher este. Testar uma estrategia e
testar dez mil e manter a melhor sao coisas enormemente diferentes: o segundo manufacture um
alto Sharpe in-sample por acaso. Alimentar a contagem honesta de trials e o ponto inteiro — isso eleva a
deflacao e pode mover um "otimo" backtest para **Overfit**. Quando o otimizador nativo do cTrader Console
landar, cMind o alimentara com o tamanho real do grid do sweep automaticamente.

## Inputs

- **Retornos periodicos** — um numero por periodo (ex. `0.01` = +1%). Ao menos dois.
- **Curva de equity / balanco** — cMind deriva os retornos simples consecutivos para voce.
- Ou execute diretamente em um backtest concluido: `POST /api/quant/integrity/backtest/{instanceId}` le a
  curva de equity do relatorio armazenado.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Retorna o veredicto, todas as metricas e a justificativa. `POST /api/quant/integrity/backtest/{id}` executa a
mesma analise em um backtest concluido seu.

## Por que e confiavel

As estatisticas sao funcoes puras no nucleo de dominio (`Core.Quant`) com zero dependencias de infraestrutura
— nao podem ser tiradas por um blip de rede, e sao fixadas por testes de vetor dourado contra formulas publicadas. A CDF normal / inversa sao aproximacoes de forma fechada
(Abramowitz-Stegun / Acklam), entao os mesmos inputs sempre geram o mesmo veredicto.
