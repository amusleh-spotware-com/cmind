---
description: "Backtest Integrity Lab — estatísticas determinísticas de nível institucional (Probabilistic & Deflated Sharpe, t-stat) que transformam um backtest bruto em um veredito Robusto / Frágil / Sobreajustado, corrigindo pela quantidade de configurações que você testou."
---

# Backtest Integrity Lab

Plataformas de varejo mostram o Sharpe ou lucro líquido de um backtest e param por aí. Instituições nunca confiam em um backtest bruto — elas perguntam se o resultado sobrevive à **correção por viés de seleção e pela quantidade de configurações testadas**. O Backtest Integrity Lab traz essa verificação para o cMind. É **matemática determinística** (sem IA, sem chamadas externas), então o veredito é reproduzível e todos os números são explicáveis.

Abra em **cBots → Integrity** (`/quant/integrity`).

## O que ele calcula

Dado uma série de retornos (ou uma curva de equidade/saldo) e a quantidade de conjuntos de parâmetros que você testou para chegar a ela, o analisador relata:

- **Índice de Sharpe** — por período e anualizado (raiz quadrada do tempo).
- **Probabilistic Sharpe Ratio (PSR)** — a confiança de que o *verdadeiro* Sharpe supera o benchmark, levando em conta o comprimento do histórico, assimetria e curtose (Bailey & López de Prado, 2012). Um histórico curto ou com cauda gorda diminui.
- **Deflated Sharpe Ratio (DSR)** — PSR medido contra um **benchmark deflacionado**: o Sharpe que você esperaria do *melhor de N tentativas aleatórias* sob a hipótese nula (o Teorema de Estratégia Falsa). Quanto mais configurações você testou, maior a barreira — isso é o que detecta sobreajuste.
- **t-statistic** da média de retorno. Seguindo Harvey, Liu & Zhu, uma vantagem genuína deve atingir **t ≥ 3.0**, não o 2.0 do livro-texto.
- **Assimetria / curtose** dos retornos, que alimentam as correções de PSR/DSR.

## O veredito

| Veredito | Significado | Regra |
|---|---|---|
| **Robusto** | A vantagem sobrevive aos testes que você executou. | DSR ≥ 95% **e** PSR ≥ 95% **e** \|t\| ≥ 3.0 |
| **Frágil** | Estatisticamente vivo, mas não de forma convincente — não aumente o tamanho apenas com base nisso. | entre os dois |
| **Sobreajustado** | Muito provavelmente um artefato do viés de seleção, não uma vantagem real. | DSR < 90% |

Cada resultado acompanha uma explicação em linguagem clara para que o "por quê" nunca fique oculto.

## Probabilidade de Sobreajuste de Backtest (entre tentativas)

Fornecer uma *contagem* de tentativas é bom; fornecer a **série real fora da amostra de cada configuração que você testou** é melhor. Cole-as na **grade de tentativas** opcional (uma série por linha) e o cMind executa a **Validação Cruzada Combinatorialmente Simétrica** (Bailey, Borwein, López de Prado & Zhu, 2015): ele divide as observações em grupos e, para cada forma de escolher metade como dentro da amostra, ele seleciona a melhor configuração dentro da amostra e verifica se esse vencedor fica na metade inferior **fora da amostra**. A **Probabilidade de Sobreajuste de Backtest (PBO)** é a fração de divisões onde o vencedor falhou em generalizar. Um PBO próximo a 0 significa que a melhor configuração é genuinamente a melhor; um PBO de 0,5 ou mais significa que seu processo de seleção está escolhendo ruído — o veredito se torna **Sobreajustado** independentemente de quão bom o vencedor parecia.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Quando o otimizador nativo do cTrader Console chegar, o cMind alimentará sua superfície de tentativas completa aqui automaticamente.

## Tentativas — o número que importa

`Trials` é **quantos conjuntos de parâmetros você testou** antes de escolher este. Testar uma estratégia e testar dez mil e manter a melhor são coisas completamente diferentes: o segundo fabrica um Sharpe alto dentro da amostra por acaso. Fornecer a contagem honesta de tentativas é todo o ponto — ela aumenta a deflação e pode mover um backtest "ótimo" para **Sobreajustado**. Quando o otimizador nativo do cTrader Console chegar, o cMind alimenta o tamanho real da grade do sweep automaticamente.

## Entradas

- **Retornos periódicos** — um número por período (por exemplo, `0.01` = +1%). No mínimo dois. O campo valida conforme você digita: conta os números válidos, sinaliza qualquer token que não seja um número e habilita **Analyze** apenas quando há pelo menos dois valores limpos (a grade de tentativas habilita **Assess overfitting** uma vez que há duas séries de quatro ou mais números cada).
- **Curva de equidade / saldo** — o cMind deriva os retornos simples consecutivos para você.
- **Direto de um backtest executado — sem copiar e colar.** Cada backtest concluído expõe um ícone de escudo **Check backtest integrity** na linha de lista **Backtest** e na visualização de detalhe da instância; um clique executa o Lab na curva de equidade armazenada dessa execução e mostra o veredito em um diálogo. O ícone é desabilitado até que o backtest seja concluído e tenha produzido um relatório, então nunca é um controle morto. Nos bastidores, isso é `POST /api/quant/integrity/backtest/{instanceId}`, que lê a curva de equidade do relatório armazenado.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Retorna o veredito, todas as métricas e a explicação. `POST /api/quant/integrity/backtest/{id}` executa a mesma análise em um backtest concluído que você possui.

## Por que é confiável

As estatísticas são funções puras no núcleo do domínio (`Core.Quant`) com zero dependências de infraestrutura — elas não podem ser derrubadas por um problema de rede e são fixadas por testes unitários de vetor dourado contra as fórmulas publicadas. O CDF/inverso normal são aproximações de forma fechada (Abramowitz-Stegun / Acklam), então as mesmas entradas sempre geram o mesmo veredito.
