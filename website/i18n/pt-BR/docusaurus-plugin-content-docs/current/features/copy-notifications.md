---
description: "Feed por dono de eventos de copia relevantes a seguranca — destino disparando disjuntor de rejeicao, violacao de protecao de conta ou regra prop, flatten de panico. Ligado por padrao…"
---

# Notificacoes operacionais de copia (Fase 2b)

Feed por dono de eventos de copia relevantes a seguranca — destino disparando disjuntor de rejeicao, violacao de protecao de conta ou regra prop, flatten de panico. **Ligado por padrao** (`App:Copy:NotificationsEnabled`, padrao `true`); defina false para silenciar. Conceito proprio no contexto Copy, separado do agregado market/AI `AlertRule`.

## Como funciona

Mesmo padrao out-of-band host→sink→drainer do log de transparencia de execucao:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notificacoes off) NullCopyNotificationSink   → descarta (no-op; motor inalterado)
             (notificacoes on)  ChannelCopyNotificationSink → canal DropOldest limitado
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  resolve dono de cada perfil, batches
                                     ▼
                            Feed CopyNotification  ◀── GET /api/copy/notifications
```

- Host `Notify(...)` nao bloqueante, nunca lanza — nunca toca DB, nunca atrasa copia.
- Drainer resolve `UserId` dono do perfil de cada notificacao; notificacao cujo perfil desapareceu (dono nao resolvivel) descartada, nao orfana.
- `CopyNotification` = feed append-only, reconhecivel por linha (nao agregado).

## O que e levantado

| Kind | Severidade | Quando |
|------|----------|---------|
| `DestinationTripped` | Aviso | Orcamento de rejeicao G8 exaurido; novas aberturas pausadas pela janela de cooldown. |
| `AccountProtectionTriggered` | Critico | Piso/teto de equity ZuluGuard violado; aberturas travadas (SellOut liquida). |
| `PropRuleBreached` | Critico | Perda diaria prop / drawdown rastreador violado; destino achatado + bloqueado pelo resto do dia. |
| `FlattenAll` | Critico | Flatten de panico executado; todo destino fechado + bloqueado. |
| `TokenInvalidated` | (reservado) | Token de um destino foi invalidado; aguardando rotacao. |

## API

- `GET /api/copy/notifications` (escopo do dono) — notificacoes recentes do usuario (mais recentes 200) em todos os perfis, mais contagem de **nao reconhecidas**.
- `POST /api/copy/notifications/{id}/acknowledge` — marcar uma como lida.

## Configuracao (`App:Copy`)

| Configuracao | Padrao | Efeito |
|---------|--------|--------|
| `NotificationsEnabled` | `true` | Emite notificacoes de seguranca + executa o drainer. `false` → sink no-op. |

## Testes

- **Unidade** (`CopyNotificationTests`) — destino disparado levanta `DestinationTripped`; panico flatten levanta `FlattenAll` no nivel do perfil. Via sink de captura.
- **Integracao** (`CopyNotificationDrainerTests`, Postgres real) — drainer resolve dono + persiste; notificacao para perfil desconhecido descartada.
- **DST** — host emite fire-and-forget com sink no-op padrao, entao suite de stress de copia permanece verde (23/23).
