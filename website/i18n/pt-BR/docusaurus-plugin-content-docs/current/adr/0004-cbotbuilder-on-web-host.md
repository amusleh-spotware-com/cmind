---
title: 0004 — CBotBuilder roda no host web em um container de sandbox
description: Por que compilações de cBot não confiáveis acontecem no host web dentro de um container SDK descartável em vez de em um nó.
---

# 0004 — `CBotBuilder` roda no host web em um container de sandbox

## Contexto

Compilar um cBot do usuário significa executar **MSBuild não confiável** — código arbitrário em tempo de compilação (targets, geradores de fonte, scripts de restore). Precisa do socket Docker para girar um container SDK. Nós executam containers de trading e não devem também ter privilégios de compilação.

## Decisão

`CBotBuilder` roda **no host web** (que já tem o socket Docker), dentro de um **container SDK descartável** com:

- um diretório `/work` bind-montado (apenas as entradas/saídas da compilação, não o filesystem do host);
- um volume compartilhado `app-nuget-cache` para desempenho de restore;
- sem acesso à rede do host além do que o restore precisa.

Então MSBuild não confiável não pode alcançar o filesystem ou rede do host. Containers de execução/backtest, por contraste, rodam em nós escolhidos por `NodeScheduler`.

## Consequências

- O privilégio de compilação (socket Docker) é confinado ao host web; nós executam apenas imagens de trading permitidas.
- Cada compilação é isolada em um container descartável — uma compilação maliciosa não pode persistir ou escapar.
- O host web deve ter um socket Docker disponível; este é um requisito de implantação, não opcional.
