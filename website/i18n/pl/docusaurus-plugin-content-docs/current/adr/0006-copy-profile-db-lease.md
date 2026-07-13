---
title: 0006 — Copy hosting is coordinated by an atomic DB lease
description: Dlaczego profile kopiowania są żądane za pośrednictwem atomowego leasingu Postgres zamiast dedykowanego koordynatora i jak to zapobiega podwójnemu kopiowaniu.
---

# 0006 — Hosting kopii jest koordynowany przez atomowy leasing bazy danych

## Kontekst

Profil kopii w trakcie działania musi być hostowany przez **dokładnie jeden** węzeł — dwa hosty na tym samym profilu oznacza każdą transakcję źródłową jest lustrzana dwukrotnie (rzeczywiste pieniądze utracone). Węzły przychodzą i odchodzą (skalowanie, awarie, rolling updates) i nie chcemy uruchamiać i utrzymywać na żywo osobnej usługi koordynatora.

## Decyzja

Każdy `CopyEngineSupervisor` żąda profili za pomocą **atomowego leasingu bazy danych** na tabeli `CopyProfiles`:

- **Claim** — atomowa `ExecuteUpdate` (lub `FOR UPDATE SKIP LOCKED` podczas limitowania na węzeł) pobiera profile, które są nieprzypisane *lub* których leasing wygasł. Atomowość oznacza dwa wyścigające się nadzorcy nigdy nie żądają tego samego wiersza.
- **Renew** — żywy węzeł odświeża swój leasing każdy cykl, aby zachował swoje roszczenie.
- **Reclaim** — leasing węzła złamanego wygasa i ocalały podnosi profil na swoim następnym cyklu (auto-leczenie). W łagodnym wyłączeniu węzeł **zwalnia** swoje leasingi natychmiast, aby failover był szybki.
- **Watchdog** — host, którego zadanie wyszło, a profil jest nadal nasz zostaje ponownie uruchomiony.
- Reconcile jest pochylony, aby uniknąć stada uderzeń `UPDATE`s w skali.

## Konsekwencje

- Brak samodzielnego koordynatora do wdrażania lub utrzymania na żywo — Postgres jest pojedynczym źródłem prawdy.
- Podwójne kopiowanie jest zapobiegane przez atomowość na poziomie wiersza, nie blokowanie na poziomie aplikacji.
- Latencja failover jest ograniczona przez TTL leasingu (minus ścieżka szybka łagodnego wydania).
- To jest ścieżka pieniędzy; jest strzeżona przez deterministyczną sekwencję naprężeń (DST) — nigdy nie osłabiaj scenariusza DST, aby go przejść.
