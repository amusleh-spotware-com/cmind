---
description: "Fatos por execucao de copia — latencia, slippage realizado, preenchimento vs falha — capturados a cada tentativa de copia, exibidos como relatorio de transparencia por perfil. Desligado por padrao…"
---

# Transparencia de execucao de copia (Fase 3)

Fatos por execucao de copia — latencia, slippage realizado, preenchimento vs falha — capturados a cada tentativa de copia,
exibidos como relatorio de transparencia por perfil. **Desligado por padrao**; habilite com
`App:Copy:TransparencyEnabled=true`. Quando desligado, mecanismo de copia byte-por-byte inalterado: host emite
para sink no-op, nada escrito.

## Como funciona

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparencia off) NullCopyEventSink   → descarta (padrao; zero custo em hot-path)
             (transparencia on)  ChannelCopyEventSink → canal em memoria limitado (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  batches a cada intervalo de drain do App
                                   ▼
                          Tabela append-only CopyExecution  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Hot path livre de I/O.** Host chama `ICopyEventSink.Record(...)` — nao bloqueante,
  nunca lanza excecao. Nunca aguarda, nunca toca DB, nunca bloqueia execucao de ordem.
- **Perda preferida sobre back-pressure.** Canal limitado (`CopyExecutionChannelCapacity`) com
  `DropOldest`: se drainer DB estagna, *mais antigo* linhas de transparencia descartadas ao inves de atrasar uma
  copia. Transparencia = telemetria best-effort, nao dependencia de trading.
- **Persistencia fora de banda.** `CopyExecutionDrainer` drena canal em batches
  (`CopyExecutionDrainBatchSize`) em `CopyExecutionDrainInterval`, escreve linhas `CopyExecution` atraves de
  `DataContext` com escopo. Flush final no shutdown.
- **Fatos, nao comandos.** `CopyExecution` = log append-only (como `InstanceLog`/`AuditLog`), nao
  agregado. Queries de leitura o consultam diretamente (CQRS-lite), agregados em memoria.

## O que e registrado

Um `CopyExecutionRecord` por tentativa de copia em um destino:

| Kind | Quando | Contem |
|------|------|---------|
| `Opened` | ordem de copia placement | simbolo, lado, volume wire, preco mestre, slippage realizado (pontos), latencia (ms) |
| `Failed` | abertura de copia lancon/recusada | simbolo, lado, volume/preco mestre, latencia, razao da falha (tipo de excecao) |

(`Closed`/`Skipped`/`Reconciled` existem no enum para expansao futura.)

## O relatorio

`GET /api/copy/profiles/{id}/transparency` (escopo do dono) retorna, sobre os 500 fatos mais recentes:

- **Resumo** — total, abertos, falhados, **taxa de preenchimento**, **latencia media (ms)**, **slippage medio (pontos)**.
- **Recentes** — fatos recentes brutos (destino, posicao fonte, simbolo, lado, volume, preco mestre,
  slippage, latencia, razao, timestamp).

## Configuracao (`App:Copy`)

| Configuracao | Padrao | Efeito |
|---------|--------|--------|
| `TransparencyEnabled` | `false` | Liga captura de fatos por copia + drainer para o no. |

Capacidade do canal, tamanho do batch de drain, intervalo de drain = constantes `CopyDefaults`
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Testes

- **Unidade** (`CopyTransparencyTests`) — abertura bem-sucedida emite fato `Opened` com
  simbolo/lado/volume/latencia corretos; abertura recusada emite fato `Failed` com razao. Via
  sink de captura.
- **Integracao** (`CopyExecutionDrainerTests`, Postgres real) — drainer persiste fatos em buffer no
  log `CopyExecution`; sink vazio nao escreve nada.
- **DST** — host fire-and-forget com sink no-op padrao, entao suite de stress deterministica permanece verde (23/23).
