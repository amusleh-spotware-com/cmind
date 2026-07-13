---
description: "To je trgovalna/finančna aplikacija: zbirka podatkov hrani trgovalne račune, profile kopiranja, izzive prop firm, verige revizij in Data Protection ključ obroč…"
---

# Varnostno kopiranje in okrevanje po nesreči

To je trgovalna/finančna aplikacija: zbirka podatkov hrani trgovalne račune, profile kopiranja, izzive prop firm,
verige revizij in Data Protection ključ obroč. Izguba pomeni izgubo denarja in prelom
regulatornih/revizijskih obveznosti. Varnostno kopirajte jo in **dokazite, da obnovitev deluje**.

## Cilji

| Metrika | Cilj | Pomen |
|---------|------|-------|
| RPO (max izguba podatkov) | ≤ 5 min | Uporabite točkovno-časovno okrevanje (continuous WAL), ne samo nočnih izvlečkov. |
| RTO (max downtime) | ≤ 1 h | Čas za obnovitev + ponovno usmeritev aplikacije na obnovljeno zbirko podatkov. |
| Hranjenje varnostnih kopij | ≥ 35 dni | Pokriva pozno odkrita popravljanja + mesečna revizijska okna. |
| Test obnovitve | mesečno | Nevarnostna varnostna kopia ni varnostna kopia. |

## Kaj je treba varnostno kopirati

1. **Postgres zbirka podatkov** — vsi app podatki (ena logična zbirka `appdb`).
2. **Data Protection ključ obroč** — vztrajan **v** zbirki podatkov
   (`PersistKeysToDbContext<DataContext>`) in PFX-šifrirana prek `App:DataProtectionCertBase64`.
   Gre z varnostno kopij zbirke podatkov, **vendar cert za zaščito + njegovo geslo
   (`App:DataProtectionCertPassword`) so skrivnosti shranjene zunaj zbirke podatkov** — varnostno kopirajte jih v
   upravljalcu skrivnosti. Brez certifikata ne morete dešifrirati skrivnosti (cTID gesla, Open API žetoni,
   skrivnosti vozlišč, AI ključ) po obnovitvi.

## Upravljana Postgres (priporočeno)

Obe obliki IaC poti določata upravljano Postgres z vgrajeno PITR — omogočite in preverite hranjenje:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): nastavite
  `backup.backupRetentionDays` (≥ 35) in `geoRedundantBackup` kjer skladnost zahteva. Obnovite z
  *Point-in-time restore* na nov strežnik, nato posodobite zbirka podatkov app connection string.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): nastavite `backup_retention_period` (≥ 35) in
  `backup_window`; ohranite avtomatizirane varnostne kopije + izbirno cross-region kopijo. Obnovite z
  *RestoreDBInstanceToPointInTime*, nato znova usmeritev app.

Upravljana PITR daje ≤ 5 min RPO brez sprememb aplikacije — aplikacija samo potrebuje nov connection string
(in obstoječa strategija ponovnih poskusov izvajanja, glej [scaling.md](../deployment/scaling.md), tolerira
cutover preglasitev).

## Self-hosted Postgres

- **Continuous archiving (PITR):** omogočite WAL arhiviranje (`archive_mode=on`, `archive_command` v
  objektno shrambo) + periodičen `pg_basebackup`. Obnovitev = obnovitev osnove + replay WAL do
  ciljnega časa. To je kar zadene RPO cilj.
- **Logicni izvozi (sekundarni):** nočni `pg_dump -Fc appdb` v off-box shrambo za prenosljivost /
  delne obnovitve. Samo ni dovolj za RPO cilj.
- Šifrirajte varnostne kopije pri miru; shranjujte izven gostitelja zbirke podatkov.

## Test obnovitve (zaženite mesečno)

1. Obnovite zadnjo varnostno kopijo (PITR v "zdaj − 10 min") v **začasno** zbirko podatkov, ne produkcijsko.
2. Usmeritev app instanco za enkraten (ali psql sejo) nanjo.
3. Preverite shemo: `dotnet ef migrations list` kaže brez čakajočih migracij, app se zažene in postane
   `/health` pripravljen.
4. **Preverite verigo revizij** je nedotaknjena in neprekinjena prek `IAuditTrailVerifier` (verižica
   `AuditChainInterceptor`) — zlomljena veriga po obnovitvi pomeni okvaro ali manipulacijo.
5. Potrdite dešifriranje skrivnosti deluje (npr. Open API avtorizacija dešifrirana) — dokazuje sta bili
   Data Protection cert + geslo pravilno obnovljena.
6. Zabeležite rezultat testa (porabljen čas proti RTO) in uničite začasno zbirko podatkov.

Avtomatizirajte korake 1–4 v CI kjer okolje dovoljuje (obnovite sejano varnostno kopijo v Testcontainer,
zaženite `dotnet ef migrations list` + verifikacija verige revizij) tako da se regresija slabe-varnostne-kopije
ujame preden jo potrebujete.

## Po realni obnovitvi

1. Obnovite zbirko podatkov (PITR v tik pred incidentom).
2. Poskrbite sta Data Protection cert + geslo enaka kot pred incidentom.
3. Znova usmeritev app `appdb` connection string; zavrtite replike.
4. Startup teče migracije pod svetovalnim zaklepom (glej scaling.md) — varno z N replikami.
5. Copy/prop-firm supervisorji pridobijo nazaj svoje lease in **resync iz brokerja** (cTrader je vir resnice), torej odprte pozicije konvergirajo avtomatsko — nič se ne zaupa iz zastarele lokalne države.
6. Preverite verigo revizij + naključno preverite nedavne trgovalne podatke.
