---
description: "cMind skaluje się w poziomie przy minimalnym wysiłku operatora. Dwa stanowe obciążenia — wykonywanie run/backtest, copy-trading — oba używają bazy danych jako punktu koordynacji, więc…"
---

# Skalowanie w poziomie

cMind skaluje się w poziomie przy minimalnym wysiłku operatora. Dwa stanowe obciążenia — wykonywanie
uruchomień/backtestów oraz copy-trading — oba używają bazy danych jako punktu koordynacji, więc dodawanie
replik nie wymaga zewnętrznego koordynatora (bez ZooKeepera, bez wyboru lidera).

## Copy-trading (samonaprawiający się lease)

Każdy węzeł uruchamia `CopyEngineSupervisor` (bramkowany przez `App:Copy:Enabled`). W każdym cyklu
rekonsylacji supervisor:

1. **Przejmuje** każdy uruchomiony profil nieprzypisany *lub* z wygasłym lease, w jednej atomowej
   operacji `UPDATE` — dwaj konkurujący supervisory nigdy nie przejmą tego samego profilu, więc profil
   jest kopiowany przez dokładnie jeden węzeł (bez podwójnych zleceń).
2. **Odnawia** lease na profilach, które hostuje.
3. Hostuje przypisane profile, przesyła rotacje tokenów dostępu do działającego hosta w miejscu (bez
   utraty strumienia zdarzeń).

Awaria węzła → przestaje odnawiać; gdy upłynie `App:Copy:LeaseTtl`, dowolny przetrwały węzeł odzyskuje
jego profile w następnym cyklu, odbudowuje stan z rekonsylacji bez duplikowania transakcji. **Skalowanie
w poziomie** = dodawanie replik; nieprzypisane/wolne profile są przejmowane automatycznie.

**Skalowanie w dół / wdrożenie rolling (S1)** = przy `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**zwalnia lease tego węzła** (`AssignedNode`/`LeaseExpiresAt` → null), aby przetrwały węzeł odzyskał
je w *następnym* cyklu rekonsylacji — **nie** po pełnym `LeaseTtl`. Tylko twarda awaria czeka na TTL.
`terminationGracePeriodSeconds` agenta copy (domyślnie 30) daje czas na zakończenie zwalniania przed
zabiciem poda.

### Pokrętła (`App:Copy`)

| Ustawienie | Domyślnie | Uwagi |
|-----------|-----------|-------|
| `Enabled` | `false` | Włącza hostowanie copy na węźle. |
| `ReconcileInterval` | `30s` | Jak często węzeł przejmuje/odnawia/rekonsyluje. |
| `LeaseTtl` | `120s` | Czas przed przejęciem profilów cichego węzła. Utrzymuj kilka interwałów rekonsylacji, aby wolny cykl nie powodował fałszywego przekazania. |
| `NodeName` | nazwa maszyny | Ustaw wyraźnie, gdy dwóch supervisorów współdzieli host. |

W Kubernetes supervisory copy działają jako Deployment; ustaw `replicas` na żądaną równoległość. Każdy
pod otrzymuje stabilną `NodeName` (domyślnie: hostname poda), więc lease są przypisane per pod. Baza
danych jest jedynym źródłem prawdy — brak sticky sessions, brak stanu per-pod do migracji.

**Zrównoważona dystrybucja (S4):** ustaw `App:Copy:MaxProfilesPerNode` > 0, aby ograniczyć liczbę
uruchomionych profilów hostowanych przez węzeł. Każdy supervisor wtedy przejmuje **co najwyżej** swoją
pozostałą rezerwę przez atomowe `FOR UPDATE SKIP LOCKED`, więc profile **rozprzestrzeniają się** na
repliki zamiast pierwszego supervisora chwytającego wszystkie — brak pojedynczego gorącego poda/SPOF.
Claim ze skip-locked zachowuje gwarancję „dokładnie jeden węzeł per profil" (bez podwójnego hostowania)
nawet przy współbieżnych przejęciach. `0` (domyślnie) = bez limitu (jeden węzeł hostuje wszystko,
bez zmian).

**Na dużą skalę (S7/S8):** każdy pod jitteruje rekonsylację o maksymalnie 20% `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`), więc N replik nie strzela jednocześnie operacjami
claim/renew `UPDATE` (postgresowa burza). Gdy `copyAgent.replicas > 1`, chart również rozprasza
repliki między węzłami (`topologySpreadConstraints`) i dodaje `PodDisruptionBudget`
(`minAvailable: 1`), więc drain/upgrade nigdy nie redukuje pojemności copy do zera.

## Uruchamianie/wykonywanie backtestów

`NodeScheduler` wybiera najmniej obciążony kwalifikujący się węzeł honorujący `MaxInstances`; zdalni
agenci węzłów rejestrują się samodzielnie i wysyłają heartbeat (`App:Discovery`), a
`NodeHeartbeatMonitor` oznacza węzeł jako nieosiągalny, gdy heartbeat przekracza `Discovery:HeartbeatTtl`.
Dodawanie agentów węzłów zwiększa pojemność wykonawczą; martwy agent jest omijany automatycznie.

## Migracje przy skalowaniu / wdrożeniu rolling

Każda replika Web/MCP uruchamia `OwnerSeeder` przy starcie, który stosuje migracje EF i zaseeda właściciela.
Aby było to bezpieczne przy jednoczesnym starcie N replik, migracja + seeding działają wewnątrz
**Postgres session advisory lock** (`MigrationLock.RunExclusiveAsync`, klucz
`DatabaseDefaults.MigrationAdvisoryLockKey`): pierwsza replika, która go nabędzie, migruruje i zaseeduje;
reszta blokuje na locku, a następnie stwierdza, że migracje już zastosowane (no-op) i właściciel już
obecny. Nie jest potrzebny osobny job migracyjny ani wybór lidera. Jeśli dodajesz seeding przy
pierwszym uruchomieniu, umieść go **wewnątrz** tego samego chronionego bloku, aby był single-writer.

## Odporność HTTP agenta węzła

Główny węzeł komunikuje się z każdym agentem `CtraderCliNode` przez HTTP przez trzy celowo rozdzielone
klienty, więc niestabilny węzeł lub sieć nigdy nie uszkodzi stanu:

- **odczyt** (`status` / `report` / `stats`) — idempotentne GET, ponawiane przy przejściowych awariach
  (exponential backoff + jitter, `NodeAgentHttp.ReadRetryCount`) z limitem czasu per próba i całkowitym.
- **zapis** (`start` / `stop` / `clean`) — nie-idempotentne POSTy, z limitem czasu ale **nigdy nie
  ponawiane**: ponowiony `start` mógłby podwójnie uruchomić kontener.
- **stream** (`logs`) — długotrwały strumień `docker logs -f` otrzymuje nieskończony timeout i brak
  pipeline'u odpornościowego, więc tailing nigdy nie jest ucinany.

Węzeł pozostający nieosiągalny jest obsługiwany przez heartbeat + [odzyskiwanie osieroconych
instancji](../operations/node-discovery.md); warstwa HTTP tylko wygładza przejściowe zakłócenia.

## Warstwy bezstanowe

Web (Blazor Server + API) i serwer MCP są bezstanowe za bazą danych, replikują się swobodnie.
Auth jest cookie-based; Web skaluje się horizontalnie za load balancerem. Serwer MCP jest osobnym
procesem/Deployment, więc skaluje się niezależnie od Web.

## Odporność połączenia z bazą danych

Każdy host otwierający bazę danych używa **retrying execution strategy**, więc przejściowe
rozłączenie lub failover managed-Postgres (RDS / Flexible Server patching) jest ponawiane zamiast
być surfacowane jako błąd użytkownika:

- Web i MCP rejestrują kontekst przez Aspire Npgsql component z `DisableRetry=false`
  i jawnym `CommandTimeout` (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (non-Aspire) rejestruje przez `UseAppNpgsql`, który stosuje to samo
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + command timeout z `DatabaseDefaults`.

Wszystkie zapisy są pojedynczymi operacjami `SaveChanges` / `ExecuteUpdate` / `ExecuteSql`, więc
retrying strategy jest bezpieczna (brak multi-statement transaction wymagającej ręcznego
`strategy.ExecuteAsync`). Jeśli dodajesz ręczną transakcję lub wiele `SaveChanges` w jednej
logicznej operacji, opakuj to w `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` —
w przeciwnym razie wyrzuci błąd podczas retry.

## Checklist skalowania

- [ ] Postgres dobrany pod dodatkowe obciążenie połączeń (każda replika Web/MCP/węzła otwiera pulę).
- [ ] `App:Copy:Enabled=true` na każdym węźle, który powinien hostować profile copy.
- [ ] Unikalna `App:Copy:NodeName` per współlokalizowany supervisor (K8s: domyślnie per-pod wystarczy).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Agenci węzłów wdrożeni tam, gdzie dostępny jest uprzywilejowany Docker (AKS/EKS/EC2/VM, nie Fargate).
- [ ] Multi-replica Web: ustaw connection string `signalr` (Redis backplane) **oraz** włącz session
      affinity na ingress (sticky sessions), aby circuit Blazor reconnects do żywego poda. Wyjątek
      komponentu jest łapany przez `MainLayout` `ErrorBoundary` (przyjazny retry, circuit pozostaje żywy).
