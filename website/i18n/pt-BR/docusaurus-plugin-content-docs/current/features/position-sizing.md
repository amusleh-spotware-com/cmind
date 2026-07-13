---
description: "Dimensionamento de posicao institucional para varejo — targeting de volatilidade e exposicao Kelly fracionario para uma unica estrategia, mais alocacao risk-parity inversa-volatilidade com matriz de correlacao atraves de um livro de estrategias."
---

# Dimensionamento de Posicao e Portfolio

"Quo grande deve ser este trade?" e a pergunta que decide se uma vantagem composita ou explode.
Instituicoes respondem com **volatility targeting** e o **Kelly criterion**, e elas constroem um livro
com **risk parity** ao inves de dolares iguais. cMind traz ambos para varejo — matematica deterministica na
serie de retornos de uma estrategia, com uma recomendacao em portugues.

Abra **cBots → Position Sizing** (`/quant/sizing`).

## Dimensionamento de estrategia unica

Dada a serie de retornos de uma estrategia (ou curva de equity), uma volatilidade anual alvo, uma fração Kelly e um
teto de alavancagem, o dimensionador reporta:

- **Volatilidade anual realizada** — a volatilidade propria da estrategia, anualizada pela regra da raiz-quadrada-do-tempo.
- **Dimensionamento por volatility-target** — a exposicao que faz a volatilidade realizada atender a sua meta
  (`alvo ÷ vol realizada`), capped no seu limite de alavancagem. Estrategias de baixa volatilidade recebem mais tamanho.
- **Kelly completo** — a fracao `f* = μ / σ²` otima de crescimento (media sobre variancia dos retornos).
- **Kelly fracionario** — `f*` escalado pela sua fracao Kelly. Half-Kelly (0.5) e a escolha comum segura;
  Kelly completo e famoso por ser agressivo demais para vantagens reais e incertas.
- **Exposicao recomendada** — o **menor** (mais seguro) dos dimensionamentos por volatility-target e Kelly fracionario,
  capped. Uma estrategia sem vantagem positiva (Kelly completo <= 0) e dimensionada para **zero**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Alocacao de portfolio

De duas ou mais estrategias (series de retorno alinhadas) ele constroi um livro por **risk parity inversa-volatilidade**
— cada estrategia ponderada por `1 / volatilidade`, normalizada — entao risco, nao dolares, e compartilhado
uniformemente. Ele tambem retorna:

- a **matriz de correlacao** entre suas estrategias (identifique as que sao secretamente a mesma aposta);
- a **volatilidade projetada do portfolio** naquelas peso, da covariancia amostral;
- um fator de **alavancagem** que escala o livro inteiro hacia sua volatilidade alvo (capped).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Por que e confiavel

Tudo e codigo de dominio puro e deterministico (`Core.Portfolio`) sem dependencia de infraestrutura e sem
chamadas externas — testado em unidade para o escalonamento vol-target, a formula de Kelly, a propriedade equal-risk de
pesos inversa-volatilidade, e a matriz de correlacao. Consultivo por padrao: os numeros sao uma
recomendacao, nunca uma ordem automatica.
