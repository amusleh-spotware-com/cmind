---
description: "To jest aplikacja handlowa/finansowa: baza danych przechowuje konta handlowe, profile kopii, wyzwania prop-firm, łańcuchy audytu i pierścień klucza Data Protection…"
---

# Kopia zapasowa i odzyskiwanie po awarii

To jest aplikacja handlowa/finansowa: baza danych przechowuje konta handlowe, profile kopii, wyzwania prop-firm, łańcuchy audytu i pierścień klucza Data Protection. Utraty go traci pieniądze i łamie obowiązki regulacyjne/audytu. Zrób kopię zapasową i **udowodnij, że przywrócenie działa**.

## Cele

| Metryka | Cel | Znaczenie |
|--------|--------|---------|
| RPO (maks. strata danych) | ≤ 5 min | Użyj point-in-time recovery (continuous WAL), nie tylko nocne zrzuty. |
| RTO (maks. przestój) | ≤ 1 h | Czas na przywrócenie + ponowne umieszczenie aplikacji na przywróconą bazę danych. |
| Retencja kopii zapasowej | ≥ 35 dni | Pokrywa późno odkrytą uszkodzenie + ponad miesiąc okna audytu. |
| Dryl przywrócenia | miesięczny | Nieprzetestowana kopia zapasowa nie jest kopią zapasową. |

## Co musi być zrobione kopią zapasową

1. **Baza danych Postgres** — wszystkie dane aplikacji (pojedyncza logiczna baza danych `appdb`).
2. **Pierścień klucza Data Protection** — utrwalony **w** bazie danych (`PersistKeysToDbContext<DataContext>`) i PFX-szyfrowany poprzez `App:DataProtectionCertBase64`. Jeździ wzdłuż kopii zapasowej DB, **ale zagrażający certyfikat + jego hasło (`App:DataProtectionCertPassword`) to sekrety przechowywane poza DB** — robić kopię zapasową w menedżerze sekretów. Bez certyfikatu nie możesz odszyfrować sekrety (hasła cTID, tokeny Open API, sekrety węzła, klucz AI) po przywróceniu.

## Zarządzany Postgres (rekomendowany)

Oba ścieżki IaC chmury dostarcza zarządzany Postgres z wbudowanym PITR — włącz i zweryfikuj retencję:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): ustaw `backup.backupRetentionDays` (≥ 35) i `geoRedundantBackup` gdzie zgodność wymaga. Przywróć za pomocą *Point-in-time restore* do nowego serwera, następnie zaktualizuj ciąg połączenia `appdb` aplikacji.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): ustaw `backup_retention_period` (≥ 35) i `backup_window`; utrzymuj automatyczne kopie zapasowe + opcjonalna kopia między regionami. Przywróć za pomocą *RestoreDBInstanceToPointInTime*, następnie wskaź aplikację ponownie.

Zarządzany PITR daje ≤ 5 min RPO bez zmian aplikacji — aplikacja tylko potrzebuje nowego ciągu połączenia (i istniejącej strategii ponowienia, patrz [scaling.md](../deployment/scaling.md), toleruje blip przejścia).

## Samodzielnie hostowany Postgres

- **Ciągłe archiwizowanie (PITR):** włącz archiwizowanie WAL (`archive_mode=on`, `archive_command` do magazynu obiektów) + okresowy `pg_basebackup`. Przywróć = przywróć kopię zapasową bazy + powtórz WAL do czasu docelowego. To jest to, co spełnia cel RPO.
- **Logiczne zrzuty (drugorzędne):** nocny `pg_dump -Fc appdb` do magazynu poza boxem do przenośności / częściowych przywróceń. Nie wystarczająca sam na osiągnięcie celu RPO.
- Szyfruj kopie zapasowe spoczynku; przechowuj poza hostem bazy danych.

## Dryl przywrócenia (uruchom miesięcznie)

1. Przywróć najnowszą kopię zapasową (PITR do "teraz − 10 min") do **scratch** bazy danych, nie produkcji.
2. Wskaż jednorazową instancję aplikacji (lub sesję psql) na nią.
3. Zweryfikuj schemat: `dotnet ef migrations list` pokazuje żadnych oczekujących migracji, aplikacja zaczyna się i staje się `/health`-gotowy.
4. **Zweryfikuj łańcuch audytu** jest nienaruszony i niezłamany poprzez `IAuditTrailVerifier` (łańcuch `AuditChainInterceptor` tamper-evident) — przerwany łańcuch po przywróceniu oznacza uszkodzenie lub manipulację.
5. Potwierdź, że odszyfrowanie sekretu działa (np. autoryzacja Open API odszyfrowuje) — potwierdza Data Protection cert + hasło zostały przywrócone poprawnie.
6. Zanotuj wynik drilu (czas podjętych vs RTO) i zniszcz scratch bazę danych.

Zautomatyzuj kroki 1–4 w CI, gdzie środowisko pozwala (przywróć zasiane kopią zapasową do Testcontainer, uruchom `dotnet ef migrations list` + weryfikacja łańcucha audytu), więc regresja złamanej kopii zapasowej jest złapana, zanim jej potrzebujesz.

## Po rzeczywistym przywróceniu

1. Przywróć DB (PITR do tuż przed incydentem).
2. Upewnij się, że Data Protection cert + hasło to **ten sam** przed incydentem.
3. Wskaż aplikację ponownie `appdb` ciąg połączenia; rzuć repliki.
4. Startup uruchamia migracje pod lock doradcy (patrz scaling.md) — bezpieczne z N replikami.
5. Nadzorcy kopii/prop-firm odzyskują ich leasingi i **resync z brokera** (cTrader to źródło prawdy), więc otwarte pozycje reconverge automatycznie — nic nie jest zaufane z zastanym stanem lokalnym.
6. Zweryfikuj łańcuch audytu + punkt-check niedawne dane handlowe.
