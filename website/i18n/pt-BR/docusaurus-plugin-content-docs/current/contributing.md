---
slug: /contributing
title: Contribuindo
description: Como contribuir para o cMind — PRs assistidas por humano ou AI são bem-vindas. Primeira contribuição em 10 minutos.
sidebar_position: 5
---

# Contribuindo para cMind 🛠️

Obrigado por estar aqui. cMind fica melhor toda vez que alguém abre uma issue, relata comportamento preciso do cTrader, corrige um typo nessas próprias docs, ou envia uma PR. **Você não precisa ser um mago de .NET** — testers, traders e doc-fixers são tão valorizados quanto o pessoal escrevendo agregados.

:::tip O guia canônico vive no repo
Esta página é a rampa amigável. O processo completo, sempre atualizado — regras base, convenções de código, fluxo de review — está em **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Sua primeira contribuição em ~10 minutos

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 avisos, ou CI vai educadamente recusar você
dotnet test           # unit + integration + E2E
```

Encontrou algo para consertar? Faça branch, mude, adicione um teste e abra uma PR. Esse é o loop inteiro.

## Maneiras de ajudar (nem todas são código)

| Contribuição | Esforço | Onde |
|---|---|---|
| 🐛 Reporte um bug reproduzível | 10 min | [Relatório de bug](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Sugira um recurso | 10 min | [Solicitação de recurso](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Melhore essas docs | 15 min | Edite em `website/docs/` e abra PR |
| 🧪 Adicione um teste faltante | 30 min | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Reporte comportamento exato do cTrader | 10 min | [Abra uma Discussão](https://github.com/amusleh-spotware-com/cmind/discussions) |

## As regras da casa (versão curta)

cMind move **dinheiro real**, então algumas coisas não são negociáveis — e honestamente, elas fazem a base de código uma alegria de trabalhar:

- **Strict Domain-Driven Design.** Lógica de negócio vive em agregados e objetos de valor, nunca em endpoints ou UI. (Há um playbook amigável para isso no repo.)
- **Três níveis de teste, cada mudança.** Unit + integration + E2E, *incluindo* caminhos de falha (conexões perdidas, pedidos rejeitados, nós mortos). Testes verdes são o preço de entrada.
- **Zero avisos.** `TreatWarningsAsErrors=true`. Idiomas modernos de C# 14.
- **Sem segredos, sem strings mágicas, nunca `DateTime.UtcNow`** (injete `TimeProvider` em vez disso).
- **Docs no mesmo commit.** Mude comportamento → atualize sua doc. Sim, isso inclui este site.

Detalhe completo, com o *porquê* por trás de cada regra, em [CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) e [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Contribuindo com AI 🤖

Nós genuinamente bem-vindos **PRs assistidas por AI** — este projeto é construído para ser trabalhado por agentes assim como por humanos. Se você está dirigindo Claude, Copilot ou similar: aponte para [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), deixe-o ler os arquivos `CLAUDE.md` aninhados e mantenha-o no mesmo padrão (testes, zero avisos, DDD). Uma boa PR de AI é indistinguível de uma boa PR humana — mesma review, mesma acolhida.

## Seja excelente um com o outro

Temos um [Código de Conduta](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md). A essência: seja gentil, assuma boa fé e lembre-se de que há uma pessoa (ou o agente de uma pessoa) do outro lado. Faça perguntas cedo — isso é uma força, não uma chatice.

Bem-vindo a bordo. Mal podemos esperar para ver o que você constrói. 🎉
