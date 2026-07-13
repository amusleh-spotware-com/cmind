---
description: "cTrader-CLI-Knoten treten dem Cluster durch Selbst-Registrierung + Heartbeat bei — kein manueller Eintrag. Gleiche Muster wie Consul/Nomad/kubeadm-Agenten: Agent startet…"
---

# Knoten-Auto-Discovery

cTrader-CLI-Knoten treten dem Cluster durch **Selbst-Registrierung + Heartbeat** bei — kein manueller Eintrag. Gleiche Muster wie Consul/Nomad/kubeadm-Agenten: Agent startet, um die Main-Knoten-Position + gemeinsamen Cluster-Geheimnis zu wissen, dann kündigt sich kontinuierlich selbst an.

> Verifiziert End-to-End auf Docker Compose und `kind` Kubernetes-Cluster: Agenten Selbst-Register, erscheinen in DB erreichbar, Auto-Mark unerreichbar, wenn Heartbeats über TTL stoppen, kehren online zurück, wenn fortgesetzt.

## Wie es funktioniert

```
CtraderCliNode Agent                         Main (Web)
------------------                         ----------
POST /api/nodes/register  ── Join-Token ──▶ Token verifizieren (konstante Zeit)
  { Name, BaseUrl, Mode,                    Protokoll-Version verifizieren
    MaxInstances, DataDir,                  Upsert CtraderCliNode nach Name
    ProtocolVersion }                       Zeitstempel LastHeartbeatAt, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  alle HeartbeatInterval            NodeHeartbeatMonitor (Hintergrund):
        └──────────────────────────────────── Wenn jetzt - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Registrierung == Heartbeat.** Agent re-POSTs auf `HeartbeatIntervalSeconds`. Der erste Anruf erstellt Knoten (`NodeRegistered`-Event); spätere Anrufe erfrischen Liveness. Fortgesetzter Heartbeat nach Ausfall flip Knoten zurück erreichbar (`NodeCameOnline`).
- **Liveness-Versöhnung.** `NodeHeartbeatMonitor` markiert Knoten, deren letzter Heartbeat `HeartbeatTtl` unerreichbar übersteigt. Scheduler (`IsActive`/`AcceptsRun`/`AcceptsBacktest` gated auf Erreichbarkeit) stoppt Platzierung von Arbeit bis sie wieder melden.
- **Verwaiste-Instanz-Rückforderung.** `NodeInstanceReclaimer` (Hintergrund) transitioniert beliebige Nicht-Terminal-Instanz stranded auf einem unerreichbaren Knoten zu **Failed** (`FailureReason = "Node unreachable - instance reclaimed"`, `InstanceFailed`-Domain-Event → Benutzer-Benachrichtigung), sodass ein abgestürzter/partitionierter Knoten niemals eine Instanz "Running" auf ewig stecken lässt. Rückforderung ist nur ausgelöst, sobald der Knoten's letzter Heartbeat stale ist über `HeartbeatTtl + InstanceReclaimGrace`, einem Kurz-Blip eine Chance zum Erholen gibt zuerst. Zurückgeforderte **Läufe werden nicht automatisch neu geplant**: ein Partitioniert-aber-am-Leben-Knoten führt möglicherweise noch den Container aus und es gibt keine Container-Level-Umzäunung, sodass neu-starten würde Doppel-Ausführungs-Risiko — der Benutzer startet eine zurückgeforderte Lauf absichtlich neu. Backtests selbst-Ausgang, daher ist ein zurückgeforderte Backtest einfach erneut laufen.
- **Identität ist Knoten-Name.** Main upserts nach `NodeName`, sodass Pod, dessen IP/URL auf Neustart ändert, hält Identität, re-registriert neue `AdvertiseUrl`.
- **Mode fix bei ersten Registrierung.** Knoten-Mode (`Run`/`Backtest`/`Mixed`) ist persistente Typ, kann nicht auf Heartbeat ändern; re-Registrierung mit anderem Mode akzeptiert für Liveness, aber Mode-Änderung ignoriert (als Warnung geloggt). Zum Ändern des Modus: Knoten löschen, ihn erneut registrieren lassen.

## Konfiguration

Main (Web) — `App:Discovery`:

| Schlüssel | Standard | Bedeutung |
|-----|---------|---------|
| `Enabled` | `false` | Master-Schalter für Register-Endpunkt + Monitor. |
| `JoinToken` | — | Gemeinsames Cluster-Geheimnis (≥ 32 Zeichen), das Agenten präsentieren müssen. |
| `HeartbeatTtl` | `00:01:30` | Gnade vor stillem Knoten als unerreichbar markiert. |
| `InstanceReclaimGrace` | `00:01:00` | Extra-Marge über `HeartbeatTtl` hinaus vor einer verwaisten Instanz auf einem unerreichbaren Knoten wird zurückgefordert (fehlgeschlagen). |
| `MonitorInterval` | `00:00:30` | Wie oft der Monitor und Instance-Reclaimer Sweep. |
| `HeartbeatInterval` | `00:00:30` | Wert an Agenten als empfohlene Kadenz zurückgegeben. |

Agent (CtraderCliNode) — `NodeAgent`:

| Schlüssel | Bedeutung |
|-----|---------|
| `MainUrl` | Basis-URL des Hauptknotens. Leer = Manueller Registrierungs-Modus (Schleife No-Op). |
| `AdvertiseUrl` | URL Main verwendet zum Erreichen **dieses** Agenten. |
| `NodeName` | Eindeutig Name; standardmäßig Maschinenname, wenn leer. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Kapazitäts-Hinweis von Scheduler beachtet. |
| `HeartbeatIntervalSeconds` | Re-Registrierungs-Kadenz. |
| `JwtSecret` | Muss Main's `JoinToken` gleich sein — beide Registrierungs-Bearer und Dispatch-JWT-Signaturschlüssel. |

## Sicherheits-Modell (v1)

Auto-Registrierte Knoten teilen **ein Cluster-Geheimnis** (`JoinToken` == jedes Agenten's `JwtSecret`). Main signiert jede Dispatch-Anfrage als 5-Minuten-HS256-JWT mit diesem Geheimnis; Agent validiert. Anforderungen:

- Halten Sie `JoinToken` ≥ 32 Zeichen und Rotation (Hauptausgabe's `App:Discovery:JoinToken` und jedes Agenten's `NodeAgent:JwtSecret` zusammen aktualisieren).
- Beenden Sie TLS vor Main und Agenten in der Produktion (Reverse Proxy / Ingress).
- Agent läuft immer noch nur Bilder, die `AllowedImagePrefix` entsprechen.

**Härtung Follow-Up (nicht v1):** Geben Sie eindeutig pro-Knoten-Geheimnis bei Registrierung (kubeadm-Stil-Bootstrap → Pro-Knoten-Anmeldedaten) auf, sodass ein einzelner kompromittierter Agent Dispatch-Token für Peers nicht fälschen kann. Registrierungs-Fluss gibt bereits Response Body zurück — natürlicher Ort um zurück-geprägt Pro-Knoten-Geheimnis zu übergeben.

## Manuelle Knoten funktionieren immer noch

`POST /api/nodes` (Admin UI) weiterhin Registrierung fixierte Knoten mit eigenem Pro-Knoten-Geheimnis. Discovery ist Zusätzlich.

Eine White-Label-Bereitstellung kann **verstecke die manuellen Steuerelemente** (oder die ganze Knoten-Oberfläche) und verlasse sich rein auf Auto-Discovery: `App:Branding:NodesUi=Monitor` lässt manuelle Hinzufügen/Löschen fallen, `Hidden` entfernt die Nav, Seite und manuellen API, und `App:Branding:RestrictNodesToOwner` Floors die Oberfläche bei nur-Besitzer. Der Selbst-Register + Heartbeat-Endpunkt hier wird in jedem Modus unbeeinträchtigt. Siehe [White-Label → Knoten-UI-Sichtbarkeit](../features/white-label.md#nodes-ui-visibility).
