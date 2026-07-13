---
title: Backup es katasztrofa helyreallitas
description: "Ez egy kereskedelmi/pénzügyi alkalmazás: az adatbázis kereskedési számlákat, masolási profilokat, prop-firm kihavásokat, audit láncokat és az Data Protection kulcs gyűrűt tartalmazza. Ha elveszik, pénzt veszítesz és a szabályozási/audit kötelezettségek megszakadnak. Készíts róla másolatot, és bizonyítsd, hogy a helyreállítás működik."
---

# Backup es katasztrofa helyreallitas

Ez egy kereskedelmi/pénzügyi alkalmazás: az adatbázis kereskedési számlákat, masolási profilokat, prop-firm kihavásokat, audit láncokat és az Data Protection kulcs gyűrűt tartalmazza. Ha elveszik, pénzt veszítesz és a szabályozási/audit kötelezettségek megszakadnak. Készíts róla másolatot, és **bizonyítsd, hogy a helyreállítás működik**.

## Celok

| Metrika | Cel | Jelentés |
|---------|-----|---------|
| RPO (max adatvesztés) | <= 5 perc | Használj point-in-time recovery-t (folyamatos WAL), ne csak éjszakai dump-okat. |
| RTO (max leállás) | <= 1 óra | A helyreállás + az alkalmazás az visszaállított adatbázisra irányítása ideje. |
| Backup megőrzés | >= 35 nap | Visszakerül egy későn felfedezett korrupció + havi audit ablakok fedezésére. |
| Helyreállítási drill | havonta | Egy nem tesztelt backup nem backup. |

## Mit muszaj menteni

1. **A Postgres adatbázis** - minden alkalmazás adat (egyetlen logikai adatbázis `appdb`).
2. **A Data Protection kulcs gyűrű** - perzisztálva **az** adatbázisban (`PersistKeysToDbContext<DataContext>`) és PFX-titkosított a `App:DataProtectionCertBase64`-tel. A védő tanúsítvány + jelszava (`App:DataProtectionCertPassword`) **titkok, az adatbázison kívül tárolva** - mentsd őket a secrets manageredbe. A tanúsítvány nélkül a titkokat nem lehet visszafejteni (cTID jelszavak, Open API tokenek, csomópont titkok, AI kulcs) a helyreállítás után.

## Managed Postgres (ajánlott)

Mindkét felhő IaC útvonal postgres-t provizionál beépített PITR-rel - engedélyezd és ellenőrizd a megőrzést:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): állítsd be a `backup.backupRetentionDays`-t (>= 35) és a `geoRedundantBackup`-ot, ahol a megfelelőség megköveteli. Állítsd vissza a *Point-in-time restore*-dal egy új szerverre, aztán frissítsd az alkalmazás `appdb` kapcsolati karakterláncát.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): állítsd be a `backup_retention_period`-ot (>= 35) és a `backup_window`-t; tartsd meg az automatizált backup-okat + opcionális cross-region másolatot. Állítsd vissza a *RestoreDBInstanceToPointInTime*-val, aztán irányítsd vissza az alkalmazást.

A managed PITR biztosítja a <= 5 perces RPO-t alkalmazás változtatások nélkül - az alkalmazásnak csak az új kapcsolati karakterláncra van szüksége (és a meglévő retrying execution strategy, lásd [scaling.md](../deployment/scaling.md), tolerálja az átmeneti blip-et).

## Self-hosted Postgres

- **Folyamatos archiválás (PITR):** engedélyezd a WAL archiválást (`archive_mode=on`, `archive_command` objektum tároláshoz) + periodikus `pg_basebackup`. Helyreállítás = base backup visszaállítás + WAL replay a cél időpontig. Ez az, ami teljesíti az RPO célkitűzést.
- **Logikai dump-ok (másodlagos):** éjszakai `pg_dump -Fc appdb` off-box tárolásra a hordozhatóság/parciális visszaállítások érdekében. Nem elég önmagában az RPO célkitűzéshez.
- A backup-okat titkosítsd nyugalmi állapotban; tárold az adatbázis gazdagépen kívül.

## Helyreállítási drill (futtasd havonta)

1. Állítsd vissza a legújabb backup-ot (PITR "most - 10 perchez") egy **scratch** adatbázisba, nem a produkcióba.
2. Irányítsd egy kidobható alkalmazás példányt (vagy egy psql session-t) rá.
3. Ellenőrizd a sémát: `dotnet ef migrations list` nem mutat függőben lévő migrációkat, az alkalmazás elindul és `/health`-re kész lesz.
4. **Ellenőrizd az audit láncot**, hogy sértetlen és töretlen az `IAuditTrailVerifier` révén (a hamisításbiztos `AuditChainInterceptor` lánc) - a helyreállítás utáni törött lánc korrupciót vagy manipulációt jelent.
5. Erősítsd meg a titkosítás visszafejtés működését (pl. egy Open API engedélyezés visszafejtése) - bizonyítja, hogy a Data Protection tanúsítvány + jelszó helyesen lett visszaállítva.
6. Jegyezd fel a drill eredményét (eltöltött idő vs. RTO) és semmisítsd meg a scratch adatbázist.

Automatizáld az 1-4 lépést CI-ben, ahol a környezet engedi (visszaállítás seedelt backup-ot Testcontainer-be, `dotnet ef migrations list` + az audit-chain verify futtatása), így egy törött-backup regresszió el van kapva, mielőtt szükséged lenne rá.

## Valódi helyreállítás után

1. Állítsd vissza az DB-t (PITR közvetlenül az incidens előtt).
2. Győződj meg róla, hogy a Data Protection tanúsítvány + jelszó **ugyanazok**, amelyeket az incidens előtt használtak.
3. Irányítsd vissza az alkalmazás `appdb` kapcsolati karakterláncát; görgess a replikákat.
4. Az indítás a migrációkat az advisory lock alatt futtatja (lásd scaling.md) - biztonságos N replikával.
5. A másolási/prop-firm felügyelők reclaim-elik a lease-eik és **szinkronizálnak a brokerből** (a cTrader a forrása az igazságnak), szóval a nyitott pozíciók automatikusan konvergálnak - a helyi állapotból semmit nem bíznak meg.
6. Ellenőrizd az audit láncot + szúrópróba a legutóbbi kereskedési adatokon.
