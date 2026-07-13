---
description: "A Open API do cTrader permite um token de acesso valido por ID cTrader (cID) por vez. No momento em que um novo token e emitido — uma atualizacao agendada, ou uma…"
---

# Ciclo de vida do token da Open API

A Open API do cTrader permite **um token de acesso valido por ID cTrader (cID) por vez**. O momento
em que um novo token e emitido — uma atualizacao agendada, ou uma re-autorizacao quando o usuario vincula outra
conta no mesmo cID — o token de acesso anterior e invalidado. Um mecanismo de copia executando em um
no remoto esta segurando esse token agora-morto, entao o novo token deve alcancalo sem interromper a
conexao ao vivo.

## Modelo

- **`OpenApiAuthorization`** e o agregado que detem tokens de acesso + refresh criptografados de um cID. Um indice unico em `(UserId, CtidUserId)` aplica **exatamente uma autorizacao por cID
  por usuario**.
- **`TokenVersion`** — um contador monotono incrementado toda vez que o token gira (`Refresh()`,
  que tambem cobre o caminho de re-autenticacao quando outra conta e vinculada no mesmo cID). E a
  marca de versao para a regra de token-unico-valido e e o que um host em execucao usa para detectar uma
  mudanca mesmo se duas strings de token chegarem a colidir.
- Tokens sao criptografados em repouso via `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`). Nunca sao logados ou armazenados em texto puro.

## Propagacao (troca harmonica no local)

1. Um token gira → o novo token + `TokenVersion` incrementada sao persistidos.
2. O `CopyEngineSupervisor` no no hospedeiro re-le o plano a cada ciclo de reconciliacao e
   calcula uma **assinatura de token** (tokens de acesso + versoes). Uma mudanca significa uma rotacao.
3. Ao inves de derrubar o host e reiniciar (o que droparia o stream de execucao do mestre),
   o supervisor **empurra o novo token para o host em execucao**.
4. O host re-autentica a conta afetada **no soquete existente**
   (`ProtoOAAccountAuthReq` novamente) via `SwapAccessTokenAsync`, entao faz uma leve reconciliacao. O
   token antigo morre; o stream de copia nunca para.

Isso e o que torna o caso cross-cID seguro: um usuario adicionando uma segunda conta do mesmo cID
no meio da execucao invalida o token antigo, e o perfil de copia em execucao continua no novo.

## Atualizacao

`OpenApiTokenRefreshService` (background) atualiza autorizacoes proativamente antes do vencimento;
`OpenApiAuthorization.IsExpiring(threshold, now)` controla. O cTrader gira o **refresh** token
em cada atualizacao, entao o novo refresh token e persistido imediatamente; um cache somente leitura que nao pode
persistir se auto-invalida (relevante para o Job de teste in-cluster, que monta uma copia gravavel
do secret).

### Escalacao de falhas

Uma atualizacao falhada nao e silenciosa. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
registra `RefreshFailedAt`, incrementa `ConsecutiveRefreshFailures`, e sempre levanta
`AccessTokenRefreshFailed` (aviso). Quando o token esta dentro de `App:OpenApi:TokenRefreshCriticalWindow`
(padrao 6h) do vencimento e a atualizacao ainda esta falhando, escalate **uma vez** com um
evento de dominio `AccessTokenRefreshCritical` + log `Critical` para que o dono possa re-autorizar antes
que operacoes de copia/prop-firm percam o token. O contador de falhas e o latch de escalacao resetam na proxima
`Refresh` bem-sucedida. O servico continua repetindo a cada `TokenRefreshInterval`, entao uma
interrupcao de provedor/manutencao se auto-cura quando o endpoint de atualizacao retorna.

## Alerta de invalidacao e auto-recuperacao (M1)

Uma autorizacao parcial/again em um cID invalida o token que um host de copia em execucao ainda detem. Quando uma
chamada de trading rejeita com `OpenApiErrorKind.TokenInvalid`, o host levanta um distinto
**`CopyTokenInvalidated`** alerta (log 1078) — nao uma falha generica — para que o canal de notificacao saiba que um
token precisa de atencao. Recuperacao e automatica: o supervisor re-le a autorizacao a cada ciclo e,
quando o token atualizado muda a assinatura de token, empurra para o host em execucao para uma **troca no local**
— copia retoma sem re-adicao manual. Um perfil `NotLinkable` (token/auth temporariamente
ina resolucao) tambem e reavaliado a cada ciclo do supervisor e hospedado no momento em que seu plano se constroi novamente.

## Watchdog de atividade do host (M2)

O supervisor observa a task de execucao de cada perfil hospedado. Se um host sai ou falha enquanto seu perfil ainda esta
atribuido a este no, o watchdog cancela e **reinicia** proximo ciclo (log
`CopyHostRestarted`), entao um host travado se auto-cura ao inves de precisar reinicio manual — e uma
falha de perfil nunca paralisa os outros (isolamento por perfil).

## Testes

- **Unidade** — `TokenVersion` incrementa em `Refresh`; host realiza troca no local sem reiniciar;
  invalidade cross-cID troca tokens fonte e destino; **um token de destino invalidado levanta
  `CopyTokenInvalidated` e auto-recupera na proxima empurrada de token** (M1); a decisao watchdog `IsHostDead`
  reinicia um host concluido/falido e deixa um perfil realocado em paz (M2).
- **Integracao** — `TokenVersion` persiste + incrementa atraves de EF em Postgres real; a assinatura de token
  muda em um bump de versao mesmo se a string e inalterada.
