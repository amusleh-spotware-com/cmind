---
slug: /for-cloud-providers
title: cMind para provedores de nuvem e VPS
description: Por que um provedor de nuvem ou VPS deve oferecer hospedagem cMind gerenciada — um produto pronto para uso, diferenciado para traders algorítmicos, brokers e empresas de prop-firm, com formas claras de monetizar computação, revendedor com marca branca e IA gerenciada.
keywords:
  - hospedagem gerenciada
  - provedor VPS
  - provedor de nuvem
  - hospedagem de plataforma de negociação
  - revendedor com marca branca
  - hospedagem de IA gerenciada
sidebar_position: 7
---

# cMind para provedores de nuvem e VPS 🖥️

Você já aluga computação. cMind é um produto pronto para uso, open-source que você pode envolver essa computação ao redor: **ofereça hospedagem cMind gerenciada** e desembarque uma carga de trabalho de alto valor, pegajosa, fome de computação — traders algorítmicos, brokers, empresas de prop-firm e comunidades de negociação que desejam a plataforma em execução sem se tornarem a equipe de operações.

:::tip TL;DR
Execute o nível stateless + Postgres + frota de nó; entregue aos clientes uma URL com marca. Monetize a assinatura, a computação, a marca branca e a IA. → [Implante na nuvem](./deployment/cloud.md)
:::

## Por que oferecer cMind gerenciado

- **Nenhum custo de construção.** É open-source, MIT-licenciado e já documentado, testado e containerizado. Você empacota e opera — você não constrói.
- **Um produto diferenciado para um nicho lucrativo.** A negociação algorítmica consume muita computação: backtests e nós ao vivo queimam CPU, que é *uso faturável* que você já vende.
- **Clientes pegajosos.** Traders que constroem e executam estratégias dentro da plataforma não churam casualmente.
- **Transforma uma ressalva em um upsell.** cMind é auto-hospedado por design — para clientes que "não querem ser a equipe de operações," *você* é a resposta.

## Quem compra cMind gerenciado de você

- **Quants individuais e traders** que querem isso hospedado. → [Para traders](./for-traders.md)
- **Brokers cTrader** executando um revendedor com marca branca para seus clientes. → [Para brokers](./for-brokers.md)
- **Empresas de prop-firm e cópia de negociação** que precisam de infraestrutura auditável e com marca.

## O que "cMind gerenciado" significa executar

Você opera três camadas; o cliente recebe uma URL web com marca:

| Camada | O que é | Onde é executado |
|---|---|---|
| Stateless (Web + MCP) | O aplicativo + API + servidor MCP | Qualquer plataforma de container, autoscalada |
| Banco de dados | PostgreSQL | Postgres gerenciado (RDS / Servidor Flexível / seu próprio) |
| Frota de nó | Constrói e executa contêineres cTrader | **VMs ou Kubernetes — precisa de Docker privilegiado** |

:::warning Uma coisa para escopar antecipadamente
Agentes de nó constroem e executam contêineres cTrader, então eles precisam de **Docker privilegiado**. Isto descarta tempos de execução de container serverless (Azure Container Apps, AWS Fargate) *para os agentes* — execute aqueles em [Kubernetes](./deployment/kubernetes.md), uma VM ou EC2. A camada stateless é executada em qualquer lugar.
:::

Guias de implantação reais, copy-paste tornam isto concreto: [visão geral da nuvem](./deployment/cloud.md) · [Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) · [Kubernetes](./deployment/kubernetes.md) · [Dimensionamento](./deployment/scaling.md).

## Como você o monetiza

- **Assinatura de hospedagem gerenciada.** Planos mensais Starter / Team / Business dimensionados por frota de nó e concorrência de backtest.
- **Metragem de uso e computação.** Billback horas de backtest, horas de nó ao vivo e armazenamento — naturalmente medido pela frota de container que você já executa.
- **Camadas de revendedor com marca branca.** Cobre mais para uma rebranding completa (logotipo, cores, PWA, `ShowSiteLink=false`) e pela habilitação de capacidades premium via [alternadores de recursos](./features/feature-toggles.md). → [Marca branca](./features/white-label.md)
- **IA gerenciada.** Agrupe uma chave de fornecedor de IA padrão para que os usuários de cada cliente obtenham IA sem configuração e marque o uso — ou ofereça trazer sua própria chave. → [Recurso de IA](./features/ai.md)
- **Compartilhamento de receita de prop-firm e cópia de negociação.** Hosts de firmas que executam desafios e taxas de desempenho e levam um corte de plataforma. → [Prop-firm](./features/prop-firm.md) · [Taxas de desempenho](./features/copy-performance-fees.md) · [Marketplace do provedor](./features/copy-provider-marketplace.md)
- **Configuração, onboarding e SLA.** Anexar serviços profissionais e suporte premium.

## Padrões multi-inquilino

- **Implantação por inquilino (recomendado).** Uma instância marcada por cliente — isolamento forte, marca por inquilino e banco de dados, token de junção de nó distinto por inquilino. A marca é lida de `IOptionsMonitor`, para que cada instância carregue sua própria identidade.
  → [Marca branca multi-inquilino](./white-label-for-business.md#multi-tenant-per-customer-branding) · [Descoberta de nó](./operations/node-discovery.md)
- **Plano de controle compartilhado (avançado).** Dirija muitas instâncias de sua própria camada de provisionamento, semeando marca e recursos por inquilino programaticamente.

## Metragem de uso para cobrança

Um ponto final **`GET /api/usage`** somente de proprietário/administrador retorna um resumo somente leitura que um provedor pode pesquisar e faturar — sem qualquer novo domínio ou persistência, ele projeta estado existente:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Pesquise por implantação de inquilino para dirigir assentamento, frota ou preços baseados em carga de trabalho. Empareche com [logging e observabilidade](./operations/logging.md) para metragem de computação mais fina.

## Mantendo margens previsíveis

Nós de escala para demanda, compartilhe camadas Postgres e autoscale a camada stateless. As superfícies operacionais que você precisa já estão lá:

- [Dimensionamento e auto-recuperação](./deployment/scaling.md)
- [Logging e observabilidade](./operations/logging.md)
- [Backup e recuperação](./operations/backup-recovery.md)

## Comece

1. Configure uma implantação de referência a partir dos [guias de nuvem](./deployment/cloud.md).
2. Modelo por inquilino (marca + token de junção + DB) e fiação sua cobrança para uso de computação.
3. Liste — você agora tem uma plataforma de negociação algorítmica gerenciada para vender.

## Contribua de volta

Provedores que executam cMind em escala atingem as arestas afiadas primeiro. Upstream de suas correções operacionais e melhorias IaC mantém sua frota barata de manter — comece com o [Guia de Contribuição](./contributing.md).
