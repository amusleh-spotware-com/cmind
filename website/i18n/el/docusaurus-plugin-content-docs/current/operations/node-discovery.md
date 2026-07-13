---
description: "cTrader CLI nodes ενώνουν το cluster με αυτοεγγραφή + heartbeat — χωρίς χειροκίνητη καταχώρηση. Το ίδιο μοτίβο με Consul/Nomad/kubeadm agents: ο agent εκκινεί γνωρίζοντας την τοποθεσία του main node…"
---

# Node auto-discovery

Τα cTrader CLI nodes ενώνουν το cluster με **αυτοεγγραφή + heartbeat** — χωρίς χειροκίνητη
καταχώρηση. Το ίδιο μοτίβο με Consul/Nomad/kubeadm agents: ο agent εκκινεί γνωρίζοντας την
τοποθεσία του main node + shared cluster secret, μετά ανακοινώνει συνεχώς τον εαυτό του.

> Verified end-to-end σε Docker Compose και `kind` Kubernetes cluster: οι agents αυτοεγγράφονται,
> εμφανίζονται στη βάση, αυτο-σημαίνονται unreachable όταν τα heartbeats σταματούν πέρα από
> το TTL, επιστρέφουν online όταν συνεχίζουν.

## Πώς λειτουργεί

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

- **Registration == heartbeat.** Ο agent κάνει re-POST στο `HeartbeatIntervalSeconds`. Η πρώτη
  κλήση δημιουργεί το node (`NodeRegistered` event)· οι επόμενες ανανεώνουν liveness. Το
  resumed heartbeat μετά από outage επαναφέρει το node reachable (`NodeCameOnline`).
- **Liveness reconciliation.** Ο `NodeHeartbeatMonitor` σημαίνει nodes των οποίων το τελευταίο
  heartbeat υπερβαίνει το `HeartbeatTtl` ως unreachable. Ο Scheduler (`IsActive`/`AcceptsRun`/
  `AcceptsBacktest` gated on reachability) σταματά να τοποθετεί εργασία μέχρι να αναφερθούν ξανά.
- **Orphaned-instance reclaim.** Ο `NodeInstanceReclaimer` (background) μεταβαίνει κάθε
  non-terminal instance που έχει εγκαταλειφθεί σε ένα unreachable node σε **Failed**
  (`FailureReason = "Node unreachable - instance reclaimed"`, `InstanceFailed` domain event →
  user notification), ώστε ένα crashed/partitioned node να μην μπορεί ποτέ να αφήσει ένα
  instance κολλημένο "Running" για πάντα. Το reclaim πυροδοτείται μόνο μία φορά αφού το
  τελευταίο heartbeat του node είναι stale πέρα από `HeartbeatTtl + InstanceReclaimGrace`,
  δίνοντας μια σύντομη ευκαιρία σε ένα brief-blip να ανακάμψει. Τα reclaimed **runs δεν
  επαναπρογραμματίζονται αυτόματα**: ένα partitioned-but-alive node μπορεί ακόμα να
  εκτελεί το container και δεν υπάρχει container-level fencing, οπότε η επανεκκίνηση θα
  κινδύνευε διπλή εκτέλεση — ο χρήστης επανεκκινεί ένα reclaimed run σκόπιμα. Τα backtests
  self-exit, οπότε ένα reclaimed backtest απλά επανεκτελείται.
- **Identity είναι node name.** Το main upserts by `NodeName`, οπότε ένα pod του οποίου το
  IP/URL αλλάζει σε restart κρατά την ταυτότητα, επανεγγράφεται με νέο `AdvertiseUrl`.
- **Mode fixed at first registration.** Το node mode (`Run`/`Backtest`/`Mixed`) είναι
  persisted type, δεν μπορεί να αλλάξει σε heartbeat· re-registration με διαφορετικό mode
  τιμάται για liveness αλλά η αλλαγή mode αγνοείται (logged ως warning). Για αλλαγή mode:
  διαγράψτε το node, αφήστε το να επανεγγραφεί.

## Διαμόρφωση

Main (Web) — `App:Discovery`:

| Key | Default | Σημασία |
|-----|---------|---------|
| `Enabled` | `false` | Master switch για register endpoint + monitor. |
| `JoinToken` | — | Shared cluster secret (≥ 32 chars) agents πρέπει να παρουσιάζουν. |
| `HeartbeatTtl` | `00:01:30` | Grace πριν το silent node σημαδευτεί unreachable. |
| `InstanceReclaimGrace` | `00:01:00` | Επιπλέον περιθώριο πέρα από `HeartbeatTtl` πριν ένα stranded instance σε ένα unreachable node reclaimεται (failed). |
| `MonitorInterval` | `00:00:30` | Πόσο συχνά ο monitor και ο instance-reclaimer sweep. |
| `HeartbeatInterval` | `00:00:30` | Τιμή επιστρέφεται στους agents ως suggested cadence. |

Agent (CtraderCliNode) — `NodeAgent`:

| Key | Σημασία |
|-----|---------|
| `MainUrl` | Base URL του main node. Empty = manual registration mode (loop no-op). |
| `AdvertiseUrl` | URL που χρησιμοποιεί το main για να φτάσει **αυτόν** τον agent. |
| `NodeName` | Unique name· defaults σε machine name αν κενό. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Capacity hint που τιμά ο scheduler. |
| `HeartbeatIntervalSeconds` | Re-register cadence. |
| `JwtSecret` | Πρέπει να ισούται με το main's `JoinToken` — και registration bearer και dispatch JWT signing key. |

## Model ασφαλείας (v1)

Τα auto-registered nodes μοιράζονται **ένα cluster secret** (`JoinToken` == κάθε agent's
`JwtSecret`). Το main υπογράφει κάθε dispatch request ως 5-minute HS256 JWT με αυτό το
secret· ο agent επικυρώνει. Απαιτήσεις:

- Κρατήστε το `JoinToken` ≥ 32 chars και περιστρέψτε το (ενημερώστε το main's
  `App:Discovery:JoinToken` και κάθε agent's `NodeAgent:JwtSecret` μαζί).
- Τερματίστε το TLS μπροστά από το main και τους agents στην παραγωγή (reverse proxy / ingress).
- Ο agent εξακολουθεί να τρέχει μόνο images που ταιριάζουν `AllowedImagePrefix`.

**Hardening follow-up (not v1):** issue unique per-node secret at registration
(kubeadm-style bootstrap → per-node credential) ώστε ένα compromised agent να μην μπορεί
να forge dispatch tokens για peers. Η registration flow ήδη επιστρέφει response body —
natural place να handed back το minted per-node secret.

## Τα manual nodes εξακολουθούν να λειτουργούν

`POST /api/nodes` (admin UI) συνεχίζει να καταχωρεί pinned nodes με own per-node secret.
Το Discovery είναι additive.

Ένα white-label deployment μπορεί να **κρύψει τα manual controls** (ή ολόκληρη τη Nodes
surface) και να βασιστεί καθαρά στο auto-discovery: `App:Branding:NodesUi=Monitor` απορρίπτει
το manual add/delete, το `Hidden` αφαιρεί το nav, page και manual API, και το
`App:Branding:RestrictNodesToOwner` περιορίζει την επιφάνεια σε owner-only. Το
αυτοεγγραφή + heartbeat endpoint είναι απρόσβλητο σε κάθε mode. Δείτε
[White-label → Nodes UI visibility](../features/white-label.md#nodes-ui-visibility).
