---
description: "Strategy Health & Alpha Decay — detecção determinística de decadência que compara o Sharpe recente de uma estratégia com seu registro anterior e localiza o maior shift de média (CUSUM change-point), retornando um veredito Saudável / Degradando / Decaído / Desconhecido."
---

# Saúde da Estratégia e Decadência de Alpha

Toda vantagem decai — a pesquisa é inequívoca de que a meia-vida de uma estratégia quant caiu de anos
para meses, portanto *adaptação supera descoberta*. O monitor de Saúde da Estratégia informa você, a partir do próprio
histórico de retornos de uma estratégia, se a vantagem ainda existe.

Abra **cBots → Strategy Health** (`/quant/health`).

## O que ele faz

Dada uma série de retornos (ou curva de equidade, mais antigo primeiro), ele:

- divide o histórico em uma metade **anterior** e **recente** e compara seus índices Sharpe;
- executa uma varredura **CUSUM change-point** para localizar a observação onde a média se deslocou mais claramente (uma
  quebra de regime), reportada apenas quando o desvio é estatisticamente notável;
- retorna um veredito:

| Veredito | Significado |
|---|---|
| **Saudável** | O desempenho recente está alinhado com (ou melhor que) o registro anterior. |
| **Degradando** | O Sharpe recente é materialmente mais fraco que o registro anterior — monitore de perto. |
| **Decaído** | A vantagem desapareceu efetivamente na janela recente — considere pausar. |
| **Desconhecido** | Histórico insuficiente para julgar. |

- **Direto de um backtest executado — sem copiar e colar.** Cada backtest concluído expõe um ícone **Check
  strategy health** na linha de lista **Backtest** e na visualização de detalhe da instância; um clique executa o
  monitor na curva de equidade armazenada dessa execução e mostra o veredito em um diálogo. O ícone é desabilitado até
  que o backtest seja concluído e tenha produzido um relatório, portanto nunca é um controle inativo. Nos bastidores, isso é
  `POST /api/quant/health/backtest/{instanceId}`, que lê a curva de equidade do relatório armazenado.

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Por que é confiável

É código de domínio puro, determinístico (`Core.Health`) sem dependência de infraestrutura e sem chamadas externas
— testado em unidade para os casos decaído, degradando, saudável e muito-curto e para a localização de change-point.
É o companheiro manual das verificações de saúde always-on que apoiam os agentes autônomos:
as mesmas estatísticas orientam o disjuntor que reduz o risco de uma estratégia ao vivo cuja vantagem está desvanecendo.
