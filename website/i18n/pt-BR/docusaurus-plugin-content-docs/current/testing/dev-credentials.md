---
description: "Todas as credenciais que os conjuntos de teste precisam vivem em um único arquivo gitignored: secrets/dev-credentials.local.json. Copie o modelo confirmado e preencha o que você"
---

# Credenciais de desenvolvimento — um arquivo para cada teste

Todas as credenciais que os conjuntos de teste precisam vivem em um único arquivo gitignored: `secrets/dev-credentials.local.json`. Copie o modelo confirmado e preencha o que você tem — cada valor é opcional e os testes que precisam de um valor ausente saltam limpamente.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# edite secrets/dev-credentials.local.json
```

## O que cada camada de teste lê

| Camada | Precisa | De |
|------|-------|------|
| **Unidade** (`tests/UnitTests`) | nada | — determinístico, sem segredos, sem rede |
| **Integração** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **Cópia ao vivo** (`tests/IntegrationTests/CopyLive`) | aplicativo OpenAPI + cache de token | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | aplicativo OpenAPI + logins cID | `OpenApi.App`, `OpenApi.Cids` |
| **E2E execução/backtest real** (`CBotRealRunBacktestTests`) | um login cID + um número de conta **demo** | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **Recursos de IA** | chave Anthropic | `Ai.ApiKey` (não definido ⇒ recursos de IA retornam desabilitado, aplicativo ainda é executado) |

## Esquema

Veja `dev-credentials.example.json` na raiz do repositório. Seções:

- `OpenApi.App` — `{ ClientId, ClientSecret }` da aplicação cTrader Open API.
- `OpenApi.Cids` — logins cTrader ID usados pelo onboarding OAuth headless. Cada entrada também carrega um array **`Accounts`** — os números de conta de negociação cTrader (o número de login/conta, por ex. `3635817`) sob esse cID que a infraestrutura de teste é permitida linkar no aplicativo e conduzir. `CBotRealRunBacktestTests` lê a primeira entrada que tem um array `Accounts` não vazio, adiciona esse cID + conta no aplicativo, depois realmente executa e faz backtest de um cBot nele. **Coloque apenas números de conta de demo aqui** — nunca uma conta ao vivo; os testes de execução/backtest colocam pedidos reais em qualquer conta que você listar. `Accounts` vazio/omitido ⇒ o teste real de execução/backtest salta limpamente.
- `OpenApi.Tokens` — o cache de token multi-cID (uma entrada por cID autorizado com seu token de atualização/acesso + lista de contas). Escrito automaticamente por onboarding e pela etapa de atualização de token; você raramente edita à mão.
- `Owner` — login do proprietário de sementes para o aplicativo em E2E.
- `Database.ConnectionString` — apenas ao apontar testes para um Postgres externo em vez de Testcontainers.
- `Ai.ApiKey` — chave de API Anthropic para os recursos de IA.

## Precedência

1. **Variáveis de ambiente** substituem tudo (ex. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — o arquivo unificado (preferido).
3. **Arquivos divididos legados** — `openapi-test-app.local.json`, `openapi-cids.local.json`, `openapi-tokens.local.json` ainda são lidos quando o arquivo unificado está ausente, para que máquinas existentes continuem funcionando. Novas configurações devem usar o arquivo único.

## Segurança

- `secrets/` e `*.local.json` são gitignored — nada aqui nunca é confirmado.
- Testes de cópia ao vivo recusam executar contra contas não-demo (contas `IsLive` são filtradas por `LiveCopyFixture`). Mantenha apenas contas de demo no cache de token.
- Execuções em cluster (Kubernetes) montam o arquivo como um Secret somente leitura; atualizações de token são mantidas na memória e o write-back somente leitura é um silencioso não-op.
