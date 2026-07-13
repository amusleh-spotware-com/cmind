---
title: Csomopont auto-felfedezes
description: "A Ctrader CLI csomopontok a klaszterhez csatlakoznak önregistracio + szivveres altal - nincs kezi bejegyzés. Ugyanaz a minta, mint a Consul/Nomad/kubeadm agensek."
---

# Csomopont auto-felfedezés

A Ctrader CLI csomópontok a klaszterhez csatlakoznak **önregisztráció + szívverés** által - nincs kézi bejegyzés. Ugyanaz a minta, mint a Consul/Nomad/kubeadm agensek: az agent tudja a fő csomópont helyét + megosztott klaszter titkot, aztán folyamatosan meghirdeti magát.

> **Ellenőrizve** end-to-end Docker Compose-on és `kind` Kubernetes klaszteren: az ügynökök önregisztrálják magukat, megjelennek a DB-ben elérhető módon, automatikusan `IsReachable=false`-ra váltanak, amikor a szívverések meghaladják a TTL-t, és visszatérnek online, amikor folytatják.

## Hogyan mukodik

```
CtraderCliNode agent                         Main (Web)
------------------                         ----------
POST /api/nodes/register  ── join token ──▶ verify token (constant-time)
  { name, baseUrl, mode,                    verify protocol version
    maxInstances, dataDir,                   upsert CtraderCliNode by name
    protocolVersion }                        stamp LastHeartbeatAt, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  every HeartbeatInterval            NodeHeartbeatMonitor (background):
        └──────────────────────────────────── if now - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Regisztráció == szívverés.** Az ügynök újra POST-ol a `HeartbeatIntervalSeconds`-kor. Az első hívás létrehozza a csomópontot (`NodeRegistered` esemény); a későbbi hívások frissítik a liveness-t. A szívverés folytatása egy kiesés után visszakapcsolja a csomópontot elérhetőre (`NodeCameOnline`).
- **Liveness egyeztetés.** A `NodeHeartbeatMonitor` elérhetetlennek jelöli azokat a csomópontokat, amelyek utolsó szívverése meghaladja a `HeartbeatTtl`-t. Az ütemező (`IsActive`/`AcceptsRun`/`AcceptsBacktest` az elérhetőségre gate-elve) addig nem helyez munkát, amíg újra nem jelentenek.
- **Árva instance reclaim.** A `NodeInstanceReclaimer` (background) **Failed** állapotba viszi bármely nem-terminális instance-ot, amely egy elérhetetlen csomóponton ragadt (`FailureReason = "Node unreachable - instance reclaimed"`, `InstanceFailed` domain esemény → felhasználói értesítés), így egy összeomlott/osztott csomópont sosem hagyhat instance-ot "Running" állapotban örökre. A reclaim csak egyszer fut le, amikor a csomópont utolsó szívverése a `HeartbeatTtl + InstanceReclaimGrace` után van, így egy rövid blipnek esélye van helyreállni. A reclaim-elt **futasok nem auto-reschedule-ódnak**: egy osztott-de-még-mindig-élő csomópont még mindig végrehajthatja a konténert és nincs konténer-szintű fencing, szóval az újraindítás kockáztatná a dupla végrehajtást - a felhasználó szándékosan indít újra egy reclaim-elt futást. A backtesztek önmagukból kilépnek, szóval egy reclaim-elt backtest egyszerűen újrafuttatható.
- **Identitás = csomópont név.** A fő az `NodeName` alapján upsertel, szóval az a pod, amelynek IP/URL-je újraindításkor változik, megtartja az identitást, újraregisztrálja az új `AdvertiseUrl`-t.
- **Mód fix az első regisztrációnál.** A csomópont módja (`Run`/`Backtest`/`Mixed`) perzisztált típus, nem változhat szívveréskor; az eltérő módú újraregisztráció az élettartamot tiszteletben tartja, de a módváltás figyelmen kívül marad (warning-ként naplózva). Módot váltani: töröld a csomópontot, hadd újraregisztrálja magát.

## Konfiguráció

Fő (Web) - `App:Discovery`:

| Kulcs | Alapértelemzés | Jelentés |
|-----|---------|---------|
| `Enabled` | `false` | Master kapcsoló a regisztráció végpont + monitor számára. |
| `JoinToken` | — | Megosztott klaszter titok (>= 32 chars), amelyet az ügynökök prezentálnak. |
| `HeartbeatTtl` | `00:01:30` | Több, mint a csomópont összeomlása előtt elérhetetlennek jelölése. |
| `InstanceReclaimGrace` | `00:01:00` | Extra margó a `HeartbeatTtl` után, mielőtt egy csomóponton megrekedt instance reclaim-eltetik (failed). |
| `MonitorInterval` | `00:00:30` | Milyen gyakran a monitor és az instance-reclaimer sweep-eli. |
| `HeartbeatInterval` | `00:00:30` | Az ügynököknek visszaadott javasolt cadence. |

Ügynök (CtraderCliNode) - `NodeAgent`:

| Kulcs | Jelentés |
|-----|---------|
| `MainUrl` | A fő csomópont base URL-je. Üres = kézi regisztrációs mód (hurok no-op). |
| `AdvertiseUrl` | URL, amelyet a fő használ, hogy **ezt** az ügynököt elérje. |
| `NodeName` | Egyedi név; alapértelemzés szerint a gép neve, ha üres. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Kapacitás hint, amelyet az ütemező tiszteletben tart. |
| `HeartbeatIntervalSeconds` | Újraregisztráció cadence. |
| `JwtSecret` | Egyenlőnek kell lennie a fő `JoinToken`-jével - mind a regisztráció bearer, mind a dispatch JWT signing kulcsa. |

## Biztonsági modell (v1)

Az önregisztrált csomópontok **egyetlen klaszter titkot** osztanak meg (`JoinToken` == minden ügynök `JwtSecret`-je). A fő minden dispatch kérést 5 perces HS256 JWT-vel aláír ezzel a titokkal; az ügynök validálja. Követelmények:

- A `JoinToken`-t >= 32 karakternyi hosszúnak és rendszeresen rotálni kell (frissítsd a fő `App:Discovery:JoinToken`-jét és minden ügynök `NodeAgent:JwtSecret`-jét együtt).
- A TLS-t produkciós környezetben a fő és az ügynökök előtt le kell zárni (reverse proxy / ingress).
- Az ügynök továbbra is csak a `AllowedImagePrefix`-nek megfelelő képeket futtatja.

**Következő keményítés (nem v1):** egyedi csomópont-titkot kibocsátani regisztrációnál (kubeadm-stílusú bootstrap → per-csomópont credential), így egy kompromittált ügynök nem tud hamis dispatch tokeneket kovácsolni társainak. A regisztrációs folyamat már visszaadja a response body-t - természetes hely egy per-csomópont titok visszaadására.

## A kézi csomópontok továbbra is működnek

`POST /api/nodes` (admin UI) folytatja a rögzített csomópontok regisztrálását saját per-csomópont titokkal. A felfedezés additív.

Egy white-label telepítés **elrejtheti a kézi vezérlőket** (vagy a teljes Csomópontok felületet) és tisztán az automatikus felfedezésre hagyatkozhat: `App:Branding:NodesUi=Monitor` eldobja a kézi hozzáadást/törlést, `Hidden` eltávolítja a nav-t, az oldalt és a kézi API-t, és az `App:Branding:RestrictNodesToOwner` padlózza a felületet csak tulajdonosra. Az önregisztrációs + szívverési végpont itt minden módban érintetlen. Lásd [White-label → Csomópontok UI láthatóság](../features/white-label.md#nodes-ui-visibility).
