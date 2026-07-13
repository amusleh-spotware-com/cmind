---
title: Execute localmente
description: Tenha o cMind funcionando na sua máquina em alguns minutos com Docker Compose (ou .NET Aspire para desenvolvimento).
sidebar_position: 1
---

# Execute o cMind localmente 🖥️

Esta é a forma mais rápida de ver o cMind funcionando — uma instância completa na sua própria máquina. Pegue um café;
você provavelmente estará conectado antes que esfrie.

:::tip O que você terá no final
Um aplicativo web funcionando em **localhost:8080**, um servidor MCP em **localhost:8081**, um banco de dados Postgres,
e um nó trabalhador local pronto para compilar e fazer backtest de cBots. Tudo na sua máquina, tudo seu.
:::

**Antes de começar, você precisa de um dos seguintes:**

- **Apenas Docker** → use a Opção A (sem SDK do .NET necessário). Recomendado para um primeiro olhar.
- **.NET 10 SDK + Docker** → use a Opção B se você quiser modificar o código.

Ambos os caminhos são multiplataforma (Windows / macOS / Linux).

## Opção A — Docker Compose (sem SDK do .NET necessário)

Pré-requisito: Docker Desktop (ou Docker Engine + plugin compose).

```bash
cp .env.example .env        # edite PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Interface web: <http://localhost:8080> (entre com o proprietário do `.env`; obrigado a alterar a senha no primeiro login).
- Servidor MCP: <http://localhost:8081/mcp>.
- Dados do Postgres persistem no volume `pgdata`; o esquema migra automaticamente na inicialização.

O contêiner web monta o soquete Docker do host (`/var/run/docker.sock`) então o construtor no navegador e o **LocalNode** semeado compilam e executam contêineres cTrader Console na sua máquina.

**Notas multiplataforma**
- Docker Desktop (Windows/macOS) expõe o soquete em `/var/run/docker.sock` — a montagem de composição funciona como está.
- Linux: certifique-se de que seu usuário possa acessar o soquete, ou execute o compose com privilégios suficientes.
- A imagem web é `linux/amd64`; em Apple Silicon o Docker a executa sob emulação.

Parar e limpar:

```bash
docker compose down          # manter dados
docker compose down -v       # também excluir o volume do banco de dados
```

## Opção B — .NET Aspire (para desenvolvimento)

Pré-requisito: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

O Aspire orquestra Postgres, Web, MCP, pgAdmin; conecta strings de conexão + OTLP; abre o painel. Defina credenciais de proprietário como parâmetros do Aspire (`OwnerEmail`, `OwnerPassword`).

Execute apenas o aplicativo web com Postgres existente:

```bash
dotnet run --project src/Web
```

## Adicionando nós trabalhadores localmente

O LocalNode semeado já executa trabalho na sua máquina. Para exercer **descoberta automática** localmente, inicie o agente de nó apontando para o aplicativo Web (veja [descoberta de nó](../operations/node-discovery.md)) com `NodeAgent:MainUrl=http://host.docker.internal:8080` e o `JoinToken` correspondente.

## Resolução de problemas 🔧

Docker tem opiniões. Aqui estão os suspeitos usuais:

| Sintoma | Causa provável e correção |
|---|---|
| `port is already allocated` em 8080/8081 | Outra coisa está usando a porta. Interrompa-a ou altere o mapeamento em `docker-compose.yml`. |
| Web inicia mas compilações/backtests falham | O soquete Docker não está montado ou acessível. No Linux, certifique-se de que seu usuário pode alcançar `/var/run/docker.sock`. |
| `permission denied` no soquete (Linux) | Adicione seu usuário ao grupo `docker` (`sudo usermod -aG docker $USER`) e faça login novamente, ou execute com privilégios suficientes. |
| Primeira execução muito lenta | A primeira compilação puxa imagens e compila — as execuções subsequentes são muito mais rápidas. Em Apple Silicon a imagem web `linux/amd64` é executada sob emulação. |
| Não consigo entrar | Verifique `OWNER_EMAIL` / `OWNER_PASSWORD` no seu `.env`. O primeiro login força uma alteração de senha. |
| Estranheza no banco de dados após atualizações | `docker compose down -v` limpa o volume para um estado limpo (você perderá dados locais). |

Ainda preso? [Abra uma Discussão](https://github.com/amusleh-spotware-com/cmind/discussions) — somos
amigáveis. Próxima parada: [implante para verdade →](./cloud.md)
