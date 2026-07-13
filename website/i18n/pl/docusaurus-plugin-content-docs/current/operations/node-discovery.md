---
description: "Węzły cTrader CLI dołączają klaster przez samodzielną rejestrację + bicie serca — brak wpisów ręcznych. Ten sam wzór co agenci Consul/Nomad/kubeadm: agent zaczyna wiedząc główną lokalizację węzła…"
---

# Auto-odkrycie węzła

Węzły cTrader CLI dołączają klaster przez **samodzielną rejestrację + bicie serca** — brak wpisów ręcznych. Ten sam wzór co agenci Consul/Nomad/kubeadm: agent zaczyna wiedząc główną lokalizację węzła + współdzielony sekret klastra, następnie stale się ogłasza.

> Zweryfikowano od końca do końca na Docker Compose i klastrze Kubernetes `kind`: agenci samodzielnie rejestrują się, pojawiają się w DB osiągalny, auto-oznaczony nieosiągalny, gdy bicia serca zatrzymują się za TTL, wznowić online, gdy wznawia.

## Jak to działa

```
Agent CtraderCliNode                         Główne (Web)
------------------                         ----------
POST /api/nodes/register  ── token join ──▶ weryfikuj token (constant-time)
  { name, baseUrl, mode,                    weryfikuj wersję protokołu
    maxInstances, dataDir,                  upserta CtraderCliNode po nazwie
    protocolVersion }                        znaczek LastHeartbeatAt, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  każdy HeartbeatInterval            NodeHeartbeatMonitor (background):
        └──────────────────────────────────── jeśli teraz - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Rejestracja == bicie serca.** Agent re-POSTy na `HeartbeatIntervalSeconds`. Pierwsze wezwanie tworzy węzeł (zdarzenie `NodeRegistered`); później wezwania odświeża żywotność. Wznowione bicie serca po awarii odwraca węzeł z powrotem osiągalny (zdarzenie `NodeCameOnline`).
- **Pogodzenie żywotności.** `NodeHeartbeatMonitor` oznacza węzły, których ostatnie bicie serca przekracza `HeartbeatTtl` nieosiągalny. Harmonogram (`IsActive`/`AcceptsRun`/`AcceptsBacktest` gated na osiągalności) zatrzymuje umieszczanie pracy, aż znowu się zgłoszą.
- **Odbiornik instancji sierocze.** `NodeInstanceReclaimer` (background) przechodzi każdą nie-terminalną instancję uwięzioną na nieosiągalnym węźle na **Nieudane** (`FailureReason = "Node unreachable - instance reclaimed"`, zdarzenie domenowe `InstanceFailed` → powiadomienie użytkownika), więc rozbity/podzielony węzeł nigdy nie może opuścić instancji uwięzioną "Running" na zawsze. Odbiornik tylko pali raz, gdy ostatnie bicie serca węzła jest stary poza `HeartbeatTtl + InstanceReclaimGrace`, dając blip szybko szansę na odzyskanie najpierw. Odebrane **uruchomienia nie są automatycznie planowane**: podzielony-ale-żywy węzeł może nadal wykonywać kontener i nie ma ogrodzenia na poziomie kontenera, więc ponowne uruchomienie ryzykowałoby podwójnym wykonaniem — użytkownik świadomie ponownie uruchamia odebrane uruchomienie. Backtesty samowyjścia, więc odebrane backtest to po prostu ponownie.
- **Tożsamość to nazwa węzła.** Główne upserty po `NodeName`, więc pod, którego IP/URL zmienia się przy ponownym uruchomieniu, utrzymuje tożsamość, re-rejestruje nowy `AdvertiseUrl`.
- **Tryb ustalony przy pierwszej rejestracji.** Tryb węzła (`Run`/`Backtest`/`Mixed`) jest utrwalony typ, nie można zmienić przy biciu serca; rejestracja z innym trybem honoru dla żywotności, ale zmiana trybu ignorowana (zalogowana jako ostrzeżenie). Aby zmienić tryb: usuń węzeł, pozwól mu ponownie się zarejestrować.

## Konfiguracja

Główne (Web) — `App:Discovery`:

| Klucz | Domyślnie | Znaczenie |
|-----|---------|---------|
| `Enabled` | `false` | Główny przełącznik dla endpoint'u rejestracji + monitora. |
| `JoinToken` | — | Współdzielony sekret klastra (≥ 32 znaki) agenci muszą zawierać. |
| `HeartbeatTtl` | `00:01:30` | Łaska zanim milczący węzeł oznaczony nieosiągalny. |
| `InstanceReclaimGrace` | `00:01:00` | Dodatkowy margines poza `HeartbeatTtl` zanim uwięziona instancja na nieosiągalnym węźle jest odebrana (nie powiodły się). |
| `MonitorInterval` | `00:00:30` | Jak często monitor i Instance-reclaimer przebiegają. |
| `HeartbeatInterval` | `00:00:30` | Wartość zwrócona agentom jako sugerowana kadencja. |

Agent (CtraderCliNode) — `NodeAgent`:

| Klucz | Znaczenie |
|-----|---------|
| `MainUrl` | Podstawowy URL głównego węzła. Pusty = tryb rejestracji ręcznej (pętla no-op). |
| `AdvertiseUrl` | URL główny używa do osiągnięcia **tego** agenta. |
| `NodeName` | Unikalna nazwa; domyślnie nazwa maszyny, jeśli pusty. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Wskazówka pojemności honorowana przez harmonogram. |
| `HeartbeatIntervalSeconds` | Kadencja re-rejestracji. |
| `JwtSecret` | Musi równać głównym `JoinToken` — zarówno obuś rejestracji jak i kluczy podpisywania JWT wysyłki. |

## Model bezpieczeństwa (v1)

Samodzielnie zarejestrowane węzły dzielą **jeden sekret klastra** (`JoinToken` == każdy `JwtSecret` agenta). Główne znaki każde żądanie wysyłki jako 5-minutowy HS256 JWT z tym sekretem; agent sprawdza. Wymagania:

- Utrzymuj `JoinToken` ≥ 32 znaki i obróć go (aktualizuj główny `App:Discovery:JoinToken` i każdy `NodeAgent:JwtSecret` agenta razem).
- Zakończyć TLS przed głównym i agentami w produkcji (reverse proxy / ingress).
- Agent nadal tylko uruchamia obrazy pasujące `AllowedImagePrefix`.

**Hardening follow-up (nie v1):** wyda unikalny sekret na węzeł przy rejestracji (bootstrap kubeadm-style → kredyt na węzeł) więc pojedynczy zagrożony agent nie może forge tokeny wysyłki dla rówieśników. Przepływ rejestracji już zwraca treść odpowiedzi — naturalne miejsce, aby zwrócić wybity tajemniczy sekret.

## Węzły ręczne nadal pracują

`POST /api/nodes` (admin UI) nadal rejestruje pinned węzły z własnym sekretem na węzeł. Odkrycie jest dodatkiem.

Wdrażanie white-label może **ukryć kontrole ręczne** (lub całą powierzchnię Nodes) i polegać czysto na auto-odkryciu: `App:Branding:NodesUi=Monitor` porzuca dodawanie/usuwanie ręczne, `Hidden` usuwa nav, stronę i API ręczne, a `App:Branding:RestrictNodesToOwner` podłogi powierzchni na właściciela-tylko. Samodzielnie rejestruj + endpoint bicia serca tutaj jest niezmieniony w każdym trybie. Patrz [White-label → Nodes UI visibility](../features/white-label.md#nodes-ui-visibility).
