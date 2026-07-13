---
description: "Este e um app de trading/financeiro: o banco de dados contem contas de trading, perfis de copia, desafios prop-firm, cadeias de auditoria e o anel de chave de Protecao de Dados…"
---

# Backup e recuperacao de desastre

Este e um app de trading/financeiro: o banco de dados contem contas de trading, perfis de copia, desafios prop-firm,
cadeias de auditoria e o anel de chave de Protecao de Dados. Perder significa perder dinheiro e quebrar
obrigacoes regulatorias/de auditoria. Faca backup e **prove que a restauracao funciona**.

## Metricas

| Metrica | Meta | Significado |
|--------|--------|---------|
| RPO (max perda de dados) | <= 5 min | Use recuperacao point-in-time (WAL continuo), nao apenas dumps noturnos. |
| RTO (max downtime) | <= 1 h | Tempo para restaurar + apontar o app para o banco restaurado. |
| Retencao de backup | >= 35 dias | Cobre corrupcao descoberta tardia + janelas de auditoria mensal. |
| Drill de restauracao | mensal | Um backup nao testado nao e um backup. |

## O que deve ser backupado

1. **O banco de dados Postgres** — todos os dados do app (banco logico unico `appdb`).
2. **O anel de chave de Protecao de Dados** — persistido **no** banco
   (`PersistKeysToDbContext<DataContext>`) e criptografado via `App:DataProtectionCertBase64`.
   Viaja junto no backup do DB, **mas o certificado de protecao + sua senha
   (`App:DataProtectionCertPassword`) sao secrets armazenados FORA do DB** — faca backup no
   seu gerenciador de secrets. Sem o certificado voce nao pode descriptografar secrets (senhas cTID, tokens Open API,
   secrets de nos, chave AI) apos uma restauracao.

## Postgres gerenciado (recomendado)

Ambos os caminhos de IaC de nuvem provisionam Postgres gerenciado com PITR built-in — habilite + verifique retencao:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): defina
  `backup.backupRetentionDays` (>= 35) e `geoRedundantBackup` onde compliance exige. Restaure com
  *Point-in-time restore* para um novo servidor, entao atualize a connection string `appdb` do app.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): defina `backup_retention_period` (>= 35) e
  `backup_window`; mantenha backups automatizados + copia cruzada opcional entre regioes. Restaure com
  *RestoreDBInstanceToPointInTime*, entao reconecte o app.

PITR gerenciado da a meta RPO de <= 5 min sem mudancas no app — o app precisa apenas da nova connection string
(e a estrategia de execucao com retry existente, veja [scaling.md](../deployment/scaling.md), tolera o blip de cutover).

## Postgres auto-hospedado

- **Arquivamento continuo (PITR):** habilite arquivamento WAL (`archive_mode=on`, `archive_command` para
  object storage) + um `pg_basebackup` periodico. Restauracao = restore base backup + replay WAL ate o
  tempo alvo. Isso atinge a meta RPO.
- **Dumps logicos (secundario):** `pg_dump -Fc appdb` noturno para storage off-box para portabilidade /
  restauracoes parciais. Nao suficiente sozinho para a meta RPO.
- Criptografe backups em repouso; armazene fora do host de banco.

## Drill de restauracao (execute mensalmente)

1. Restaure o backup mais recente (PITR para "agora - 10 min") em um banco **temporario**, nao producao.
2. Aponte uma instancia de app descartavel (ou uma sessao psql) para ele.
3. Verifique esquema: `dotnet ef migrations list` mostra nenhuma migracao pendente, app inicia e fica
   `/health`-pronto.
4. **Verifique a cadeia de auditoria** esta intacta e ininterrupta via `IAuditTrailVerifier` (a cadeia
   `AuditChainInterceptor` a prova de adulteracao) — uma cadeia quebrada apos restauracao significa corrupcao ou adulteracao.
5. Confirme descriptografia de secrets funciona (ex. uma autorizacao Open API descriptografa) — prova que o cert de Protecao de Dados + senha foram restaurados corretamente.
6. Registre o resultado do drill (tempo vs RTO) e destrua o banco temporario.

Automatize passos 1-4 em CI onde o ambiente permite (restaure um backup com seed em um Testcontainer,
execute `dotnet ef migrations list` + a verificacao da cadeia de auditoria) para que uma regressao de backup quebrado seja pega
antes de precisar.

## Apos uma restauracao real

1. Restaure DB (PITR para logo antes do incidente).
2. Garanta que o cert de Protecao de Dados + senha sejam os **mesmos** em uso antes do incidente.
3. Reconecte a connection string `appdb` do app; rode as replicas.
4. Startup executa migracoes sob o lock consultivo (veja scaling.md) — seguro com N replicas.
5. Supervisores de copia/prop-firm reclamam seus leases e **ressincronizam do corretor** (cTrader e a
   fonte da verdade), entao posicoes abertas convergem automaticamente — nada e confiado de estado local estagnado.
6. Verifique cadeia de auditoria + spot-check dados de trading recentes.
