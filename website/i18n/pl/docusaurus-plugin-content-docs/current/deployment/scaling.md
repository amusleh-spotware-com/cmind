---
description: "cMind skaluje się w poziomie z minimalnym wysiłkiem operatora. Dwa obciążenia stanowe — wykonanie uruchomienia/backtestu, kopiowanie transakcji — oba używają bazy danych jako punkt koordynacji, więc…"
---

# Skalowanie poziome

cMind skaluje się w poziomie z minimalnym wysiłkiem operatora. Dwa obciążenia stanowe — wykonanie uruchomienia/backtestu, kopiowanie transakcji — oba używają bazy danych jako punkt koordynacji, więc dodawanie replik nie wymaga zewnętrznego koordynatora (bez ZooKeeper, bez wyborów lidera).

## Kopiowanie transakcji (auto-leczenie leasingu)

Każdy węzeł uruchamia `CopyEngineSupervisor` (gated na `App:Copy:Enabled`). Co cykl pogodzenia, nadzorca:

1. **Żąda** każdego biegającego profilu przydzielonego *lub* leasingu wygasłego, w jednym atomowym `UPDATE` — dwa wyścigające się nadzorcy nigdy nie żądają tego samego profilu, więc profil skopiowany przez dokładnie jeden węzeł (brak podwójnych zamówień).
2. **Odnawia** leasing na profilech, które hostuje.
3. Hostuje przydzielone profile, przesuwa rotacje tokenów dostępu do uruchomionego hosta w miejscu (brak upuszczenia strumienia zdarzeń).

Węzeł crash → przestaje odnawiać; po `App:Copy:LeaseTtl` mija, każdy ocalały węzeł odzyskuje jego profile następny cykl, przebudowuje stan z pogodzenia bez duplikowania transakcji. **Skalowanie w** = dodaj repliki; niezasignowane/wolne profile podnoszone automatycznie.

**Łagodne skalowanie lub rolling update (S1)** = na `SIGTERM`, `CopyEngineSupervisor.StopAsync` **zwalnia leasingi tego węzła** (`AssignedNode`/`LeaseExpiresAt` → null), aby ocalały odzyskał je jego *bardzo następny* cykl pogodzenia — **nie** po pełnym `LeaseTtl`. Tylko twarda awaria czeka pełny TTL. `terminationGracePeriodSeconds` agent kopii (domyślnie 30) daje czas zwolnienia na zanim pod został zabity.

### Gałki (`App:Copy`)

| Ustawienie | Domyślnie | Notatki |
|---------|---------|-------|
| `Enabled` | `false` | Włącz hosting kopii dla węzła. |
| `ReconcileInterval` | `30s` | Jak często węzeł żąda/odnawia/pogadzenie. |
| `LeaseTtl` | `120s` | Łaska zanim milczący profil węzła odzyskane. Utrzymuj kilka interwałów pogodzenia, aby powolny cykl nie powodował śmieciowych oddawania. |
| `NodeName` | nazwa maszyny | Ustaw odrębnie, gdy dwa nadzorcy dzielą host. |

Na Kubernetes nadzorcach kopii uruchamiają się jako Deployment; ustaw `replicas` do żądanego paralelizmu. Każdy pod uzyskuje stabilny `NodeName` (domyślnie: nazwa hosta pod), więc leasingi przypisane na pod. Baza danych jest pojedynczym źródłem prawdy — brak sesji lepkich, brak stanu na pod do migracji.

**Zrównoważona dystrybucja (S4):** ustaw `App:Copy:MaxProfilesPerNode` > 0 do limitu ile profilów biegających węzeł hostuje. Każdy nadzorca następnie żąda **co najwyżej** jego pozostałej pojemności poprzez atomową `FOR UPDATE SKIP LOCKED` ograniczoną pretensję, więc profile **rozprowadzą** między repliki zamiast pierwszego nadzorcy przechwytającą wszystko — brak jedynego gorącego pod / SPOF. Pretensja skip-locked utrzymuje "dokładnie jeden węzeł na profil" gwarancję (brak podwójnego-hosting) nawet pod równoczesną pretensjami. `0` (domyślnie) = niestalowny (jeden węzeł hostuje wszystko, bez zmian).

**W skali (S7/S8):** każdy pod pochyla pogodzenie o do 20% `ReconcileInterval` (`CopyEngineSupervisor.JitteredInterval`) co N replik nie pala pretensji/odnawia `UPDATE`s równocześnie (Postgres pęd stada). Kiedy `copyAgent.replicas > 1` chart także rozprowadza repliki przez węzły (`topologySpreadConstraints`) i dodaje `PodDisruptionBudget` (`minAvailable: 1`) więc dren/upgrade nigdy nie zabiera pojemności kopii do zera.

## Wykonanie uruchomienia/backtestu

`NodeScheduler` wybiera najmniej załadowany węzeł uprawniony honoru `MaxInstances`; agenci zdalnych węzłów samodzielnie rejestrują się i biją serce (`App:Discovery`), `NodeHeartbeatMonitor` oznacza węzeł nieosiągalny, gdy bicie serca przekracza `Discovery:HeartbeatTtl`. Dodaj agentów węzłów aby dodać pojemność wykonania; martwi agenci trasowani automatycznie.

## Migracje na skalowanie/rolling deploy

Każda replika Web/MCP uruchamia `OwnerSeeder` przy uruchomieniu, która stosuje migracje EF i nasadza właściciela. Aby uczynić to bezpieczne, gdy N replik zaczynają jednocześnie, migruj + nasadź uruchamiać wewnątrz **lock doradcy sesji Postgres** (`MigrationLock.RunExclusiveAsync`, klucz `DatabaseDefaults.MigrationAdvisoryLockKey`): pierwsza replika do przechwycenia lock'a migruje i nasadza; reszta bloku na lock, potem znaleźć migracje już zastosowane (no-op) i właściciel już obecny. Żaden osobny job migracji lub wybór lidera nie jest potrzebny. Jeśli dodasz nasadzanie pierwszego uruchomienia, umieść go **wewnątrz** tego samego pilnowanego bloku, więc jest pojedynczym pisarzem.

## Odporność HTTP agenta węzła

Główny węzeł rozmawia z każdym agentem `CtraderCliNode` przez HTTP poprzez trzy klientów podzielone na cel, więc flaky węzeł lub sieć nigdy nie portuje stan:

- **czytaj** (`status` / `report` / `stats`) — idempotentne GETy, ponowione na przejściowe awarie (exponential backoff + jitter, `NodeAgentHttp.ReadRetryCount`) z limitami czasu na próbę i całością.
- **pisz** (`start` / `stop` / `clean`) — non-idempotentne POSTy, limit czasu ale **nigdy ponowione**: ponowiona `start` może podwójnie uruchomić kontener.
- **strumień** (`logs`) — długotrwały strumień `docker logs -f` uzyskuje nieskończony timeout i nie ma rurociągu odporności, więc tailowanie nigdy nie jest odcięte.

Węzeł, który pozostaje nieosiągalny jest obsługiwany poprzez bicie serca + [odbiornik instancji sierocze](../operations/node-discovery.md); warstwa HTTP tylko wygładza przejściowe zatrzaśnięcia.

## Warstwy bezstanowe

Web (Blazor Server + API) i serwer MCP są bezstanowe za bazą danych, replikują się swobodnie. Auth jest cookie-oparta; skaluj Web poziomo za load balancer. Serwer MCP jest osobnym procesem/Deployment, więc skaluje się niezależnie od Web.

## Odporność połączenia bazy danych

Każdy host, który otwiera bazę danych, używa **strategii ponowienia** więc przejściowe rozłączenie lub failover zarządzanej-Postgres (RDS / Flexible Server patching) jest ponowiony zamiast być powierzchnią jako błąd dla użytkownika:
