---
description: "Questa è un'app di trading/finanza: il database contiene conti di trading, profili di copia, sfide prop-firm, catene di audit e l'anello chiave della protezione dei dati..."
---

# Backup e disaster recovery

Questa è un'app di trading/finanza: il database contiene conti di trading, profili di copia, sfide prop-firm, catene di audit e l'anello chiave della protezione dei dati. Perderlo significa perdere denaro e rompere gli obblighi normativi/audit. Fai il backup e **prova che il ripristino funziona**.

## Obiettivi

| Metrica | Obiettivo | Significato |
|--------|--------|---------|
| RPO (perdita max di dati) | ≤ 5 min | Usa il recupero point-in-time (WAL continuo), non solo dump notturni. |
| RTO (downtime max) | ≤ 1 h | Tempo per il ripristino + re-puntamento dell'app al database ripristinato. |
| Conservazione del backup | ≥ 35 giorni | Copre un'anomalia scoperta in ritardo + finestre di audit mensili. |
| Esercitazione di ripristino | mensile | Un backup non testato non è un backup. |

## Cosa deve essere sottoposto a backup

1. **Il database Postgres** — tutti i dati dell'app (database logico singolo `appdb`).
2. **L'anello chiave della protezione dei dati** — persistito **nel** database (`PersistKeysToDbContext<DataContext>`) e crittografato con PFX tramite `App:DataProtectionCertBase64`. Viene fornito nel backup del database, **ma il certificato protettivo e la sua password (`App:DataProtectionCertPassword`) sono segreti archiviati al di fuori del database** — esegui il backup nel tuo gestore di segreti. Senza il certificato non puoi decrittografare i segreti (password cTID, token Open API, segreti di nodo, chiave AI) dopo un ripristino.

## Postgres gestito (consigliato)

Entrambi i percorsi IaC del cloud forniscono Postgres gestito con PITR integrato — abilita e verifica la conservazione:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): impostare `backup.backupRetentionDays` (≥ 35) e `geoRedundantBackup` dove la conformità lo richiede. Ripristina con *Point-in-time restore* su un nuovo server, quindi aggiorna la stringa di connessione `appdb` dell'app.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): impostare `backup_retention_period` (≥ 35) e `backup_window`; mantieni i backup automatizzati + copia opzionale tra regioni. Ripristina con *RestoreDBInstanceToPointInTime*, quindi ri-punta l'app.

PITR gestito dà il RPO ≤ 5 min senza modifiche all'app — l'app ha solo bisogno della nuova stringa di connessione (e la strategia di esecuzione di retry esistente, vedi [scaling.md](../deployment/scaling.md), tollera il blip di cutover).

## Postgres self-hosted

- **Archivio continuo (PITR):** abilita l'archivio WAL (`archive_mode=on`, `archive_command` nell'archiviazione oggetti) + un `pg_basebackup` periodico. Ripristino = ripristino backup di base + replay WAL al tempo di destinazione. Questo è quello che soddisfa l'obiettivo RPO.
- **Dump logici (secondario):** `pg_dump -Fc appdb` notturno nell'archiviazione off-box per portabilità / ripristini parziali. Non sufficiente da solo per l'obiettivo RPO.
- Crittografa i backup a riposo; archivio fuori dall'host del database.

## Esercitazione di ripristino (esegui mensile)

1. Ripristina il backup più recente (PITR a "now − 10 min") in un database **scratch**, non produzione.
2. Punta un'istanza app usa e getta (o una sessione psql) ad essa.
3. Verifica lo schema: `dotnet ef migrations list` non mostra migrazioni in sospeso, l'app inizia e diventa `/health`-ready.
4. **Verifica che la catena di audit** sia intatta e ininterrotta tramite `IAuditTrailVerifier` (la catena `AuditChainInterceptor` antimanomissione) — una catena rotta dopo il ripristino significa corruzione o manomissione.
5. Conferma che la decrittazione segreta funziona (ad es. un'autorizzazione Open API si decripta) — prova che il certificato della protezione dei dati + password sono stati ripristinati correttamente.
6. Registra il risultato dell'esercitazione (tempo impiegato vs RTO) e distruggi il database scratch.

Automatizza i passaggi 1–4 in CI dove l'ambiente lo consente (ripristina un backup seminato in un Testcontainer, esegui `dotnet ef migrations list` + la verifica della catena di audit) in modo che una regressione di backup interrotto venga rilevata prima di averne bisogno.

## Dopo un ripristino reale

1. Ripristina il database (PITR prima dell'incidente).
2. Assicurati che il certificato della protezione dei dati + la password siano gli **stessi** in uso prima dell'incidente.
3. Ri-punta la stringa di connessione `appdb` dell'app; arrotola i replica.
4. L'avvio esegue le migrazioni sotto il blocco consultivo (vedi scaling.md) — sicuro con N replica.
5. I supervisori di copia/prop-firm riclamano i loro lease e **risincronizzano dal broker** (cTrader è la fonte di verità), quindi le posizioni aperte riconvergono automaticamente — nulla è attendibile dallo stato locale stantio.
6. Verifica la catena di audit + spot-check i dati di trading recenti.
