---
description: "Toto je obchodný/finančný app: databáza drží obchodné účty, kopírovať profily, prop-firm výzvy, audit reťazce, a Data Protection kľúčenka…"
---

# Záloha & disaster recovery

Toto je obchodný/finančný app: databáza drží obchodné účty, kopírovať profily, prop-firm
výzvy, audit reťazce, a Data Protection kľúčenka. Stratu to stratu peniaze a zlomenia
regulačný/audit povinnosti. Zálohujte to a **dokázať obnovenie funguje**.

## Ciele

| Metrika | Cieľ | Zmysel |
|--------|--------|---------|
| RPO (max dáta strata) | ≤ 5 min | Používajte point-in-time recovery (nepretržité WAL), nie iba nočný výpisy. |
| RTO (max prestoj) | ≤ 1 h | Čas na obnovenie + re-point aplikáciu na obnovenú databázu. |
| Záloha retenčné | ≥ 35 dni | Pokrýva neskoro-objavené korupcia + mesiac audit okná. |
| Obnovenie cvičenie | mesiac | Neotestovaný zálohu nie je zálohu. |

## Čo musí byť zálohovaný

1. **Postgres databáz** — všetky app dáta (jeden logický databáz `appdb`).
2. **Data Protection kľúčenka** — trvalé **v** databáz
   (`PersistKeysToDbContext<DataContext>`) a PFX-šifrované cez `App:DataProtectionCertBase64`.
   To jazdí spolu v DB zálohu, **ale chránit certifikát + Its heslo
   (`App:DataProtectionCertPassword`) sú tajomstvá uložené mimo DB** — zálohujte ich v vašom
   tajomstvo manažér. Bez certifikát vy nemôžete dešifrovať tajomstvá (cTID hesló, Open API tokeny,
   uzol tajomstvo, AI kľúč) po obnovenie.

## Spravovaný Postgres (odporúčaný)

Oba cloud IaC cesty zriaďujú spravovaný Postgres s vstavaným PITR — povoliť + overiť retenčné:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): nastaviť
  `backup.backupRetentionDays` (≥ 35) a `geoRedundantBackup`, kde compliance vyžaduje. Obnovenie
  s *Point-in-time restore* na nový server, potom aktualizácia app `appdb` connection string.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): nastaviť `backup_retention_period` (≥ 35) a
  `backup_window`; udržiavať automatizovaný zálohy + voliteľný cross-region kópie. Obnovenie s
  *RestoreDBInstanceToPointInTime*, potom repoint aplikáciu.

Spravovaný PITR dáva ≤ 5 min RPO bez app zmeny — aplikácia iba potreby nový connection string
(a existujúce opakujú vykonávanie stratégia, vidieť [scaling.md](../deployment/scaling.md), toleruje
cutover blip).

## Self-hosted Postgres

- **Nepretržité archiving (PITR):** povoliť WAL archiving (`archive_mode=on`, `archive_command` na
  objekt úložisko) + periodický `pg_basebackup`. Obnovenie = obnovenie základný zálohu + replay WAL na
  cieľ čas. Toto je, čo sa stretáva RPO cieľ.
- **Logické výpisy (sekundárny):** nočný `pg_dump -Fc appdb` na off-box úložisko na prenosnosť /
  čiastočný obnov. Nie dosť samotný na RPO cieľ.
- Šifrujte zálohy v pokoji; úložisko mimo databáz hostiteľ.

## Obnovenie cvičenie (spustiť mesiac)

1. Obnovenie najnovšia zálohu (PITR na "teraz − 10 min") do a **scratch** databáz, nie výroby.
2. Bod throwaway aplikáciu inštancia (alebo psql sedenie) na to.
3. Overiť schéma: `dotnet ef migrations list` ukazuje žádny čakajúci migrácie, aplikácia začína a osáva
   `/health`-ready.
4. **Overiť audit reťazec** je intaktný a neprasknutý cez `IAuditTrailVerifier` (tamper-evident
   `AuditChainInterceptor` reťazec) — zlomený reťazec po obnovenie znamená korupcia alebo tampering.
5. Potvrdiť tajomstvo dešifrovanie funguje (napr. Open API autorizácia dešifruje) — dokazuje Data
   Protection certifikát + heslo boli obnovené správne.
6. Záznam cvičenie výsledok (čas prevzatý vs RTO) a zničiť scratch databázu.

Automatizovať kroky 1–4 v CI, kde prostredie umožňuje (obnovenie osemené zálohu do Testcontainer,
spustiť `dotnet ef migrations list` + audit-chain overiť) takže zlomený-zálohu regressie je chytený
pred vami potreba to.

## Po reálny obnovenie

1. Obnovenie DB (PITR na len pred incident).
2. Zabezpečiť Data Protection certifikát + heslo sú **rovnaký** než pred incident.
3. Repoint aplikácia `appdb` connection string; roll repliky.
4. Startup beží migrácie pod poradný zámka (vidieť scaling.md) — bezpečný s N repliky.
5. Kopírovať/prop-firm supervisorov reclaim ich leases a **resync z makléř** (cTrader je
   zdroj pravdy), takže otvorené pozície reconverge automaticky — nič je dôveryhodný z zastaraný miestny
   stav.
6. Overiť audit reťazec + spot-kontrola nedávny obchodný dáta.
