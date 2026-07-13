---
slug: /for-brokers
title: cMind para corretoras cTrader
description: Por que uma corretora cTrader deve executar um cMind white-label para seus proprios clientes — dar a traders AI, copy trading e desafios prop-firm sob sua marca, restringir contas a sua corretagem e ganhar vantagem sobre concorrentes.
keywords:
  - corretora cTrader
  - plataforma de trading white-label
  - tecnologia de corretora
  - copy trading para corretoras
  - ferramentas de trading AI
  - software prop firm
sidebar_position: 6
---

# cMind para corretoras cTrader 🏦

Voce opera uma corretora cTrader. Seus clientes ja podem negociar — mas tambem os clientes de qualquer outra corretora.
**cMind permite que voce entregue a seus traders uma plataforma completa de operacoes de trading com AI, sob sua propria marca**,
entao eles constroem, backtest, executam, copiam e monitoram estrategias dentro do *seu*
ecossistema ao inves de derivar para uma ferramenta de terceiros. Isso significa clientes mais fixos, mais volume e uma vantagem real sobre
corretoras que oferecem apenas um terminal.

:::tip TL;DR
Execute um cMind white-label para seus clientes. Restrinja contas a **sua** corretagem, ative AI e
copy trading, e faca o ship sob sua marca. → [White-label para negocios](./white-label-for-business.md)
:::

## A vantagem que voce obtem sobre outras corretoras

- **Diferencie-se em ferramentas, nao apenas em spreads.** Ofereca aos clientes geracao de cBot com AI, backtesting em um
  cluster gerenciado, copy trading e desafios prop-firm — capacidades que a maioria das corretoras simplesmente nao
  oferecem.
- **Mantenha clientes no seu ecossistema.** Quando traders constroem e executam suas estrategias dentro da sua plataforma com marca,
  eles ficam. Retencao e o jogo inteiro.
- **Sob sua marca, no seu dominio.** Nome, logo, cores, favicon, ate o app de telefone instalavel —
  tudo seu. Ninguem ve "cMind." → [Funcionalidade white-label](./features/white-label.md)

## Sirva apenas suas contas (lista de corretoras permitidas)

Executando um white-label para *seus* clientes? Restrinja quais contas de trading de quais corretoras os usuarios podem adicionar para que
seu deployment sirva apenas o seu livro:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Nome da Sua Corretagem"]
    }
  }
}
```

Quando a lista de permitidos e definida, cMind verifica cada conta que um usuario tenta adicionar — tanto via cTrader Open
API quanto via login manual de cID (verificado lendo o nome real da corretora da conta) — e rejeita qualquer
conta que nao esta na sua lista. Deixe vazio e toda corretora e permitida (o padrao). Veja o
[doc de funcionalidade white-label](./features/white-label.md#broker-allowlist) para a mecanica completa.

## Faca ship de um unico app Open API para todos os seus usuarios

Pule o trabalho por-usuario: forneca **um aplicativo cTrader Open API** e todo cliente autoriza
suas contas atraves dele — nenhum cliente registra o seu proprio. Registre uma unica URL de redirect, coloque
as credenciais em config ou nas configuracoes do owner, e o modo compartilhado ativa para todos. Negociou um
limite de mensagem cTrader mais alto? Ajuste os **limites de taxa de cliente por tipo de mensagem** (ou desabilite o pacing).
→ [Aplicativo Open API compartilhado e limites de taxa](./features/open-api-shared-app.md)

## Novas formas de monetizar

- **AI, com zero atrito para clientes.** Forneca uma chave de provedor AI padrao no nivel do deployment e
  todo cliente obtem funcionalidades AI instantaneamente — sem inscricao em outro lugar. Marque o preco, ou faca bundle em
  camadas premium. Clientes ainda podem trazer sua propria chave. → [Funcionalidade AI](./features/ai.md)
- **Desafios prop-firm.** Execute desafios de traders financiados com rastreamento de equity live e regras impostas,
  e cobre pelas entradas. → [Regras prop-firm](./features/prop-firm.md)
- **Negocio de copy-trading.** Taxas de performance e um marketplace de provedores transformam copy trading em
  receita. → [Taxas de performance](./features/copy-performance-fees.md) ·
  [Marketplace de provedores](./features/copy-provider-marketplace.md)
- **Camadas de funcionalidade.** Decida quais capacidades cada segmento de cliente ve com
  [toggles de funcionalidade](./features/feature-toggles.md).

## Regulamentado, auditavel, multi-tenant

- **[Conformidade](./features/compliance.md)** logs oferecem a trilha de auditoria que seu regulador pedira.
- **[Autenticacao de dois fatores](./features/two-factor-auth.md)** pode ser tornada obrigatoria por deployment.
- **Marca por cliente** — execute uma instancia separada com marca por segmento, dirigida pelo seu proprio plano de
  controle. → [Marca multi-tenant](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Como comecar

1. Leia [White-label para negocios](./white-label-for-business.md) para o rebrand de 60 segundos.
2. Defina `App:Accounts:AllowedBrokers` para sua corretagem e escolha seu [conjunto de funcionalidades](./features/feature-toggles.md).
3. [Faca deploy](./deployment/cloud.md) — Docker, Kubernetes, Azure ou AWS.

Nao quer operar a infraestrutura voce mesmo? Um provedor de hospedagem pode operar um cMind gerenciado para voce
— indique-os para [Para provedores de cloud e VPS](./for-cloud-providers.md).

## Influence o roadmap

cMind e open source. Corretoras que constroem nele obtem uma voz desproporcional em para onde ele vai — solicite as
integracoes e controles que voce precisa, e contribua de volta via o
[Guia de Contribuicao](./contributing.md).
