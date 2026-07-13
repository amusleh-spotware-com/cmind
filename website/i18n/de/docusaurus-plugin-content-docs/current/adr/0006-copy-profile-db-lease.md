---
title: 0006 – Copy-Hosting wird durch einen atomaren DB-Lease koordiniert
description: Warum Copy-Profile über einen atomaren Postgres-Lease beansprucht werden, anstatt einen dedizierten Koordinator zu verwenden, und wie das Double-Copying verhindert.
---

# 0006 – Copy-Hosting wird durch einen atomaren DB-Lease koordiniert

## Kontext

Ein laufendes Copy-Profil muss von **genau einem** Node gehostet werden – zwei Hosts im gleichen Profil bedeutet, jeder Source-Trade wird zweimal gespiegelt (echtes Geld verloren). Nodes kommen und gehen (Skalierung, Crashes, Rolling Updates) und wir wollen einen separaten Koordinator-Service nicht laufen und am Leben halten.

## Entscheidung

Jeder `CopyEngineSupervisor` beansprucht Profile mit einem **atomaren DB-Lease** auf der `CopyProfiles`-Tabelle:

- **Claim** – ein atomarer `ExecuteUpdate` (oder `FOR UPDATE SKIP LOCKED`, wenn pro-Node kappend) nimmt Profile, die unzugewiesen *oder* deren Lease abgelaufen sind. Atomarität bedeutet zwei racing Supervisors beanspruchen nie beide die gleiche Row.
- **Renew** – ein Live-Node erneuert seinen Lease jeden Zyklus, daher behält er seinen Anspruch.
- **Reclaim** – ein Crashed-Node Lease läuft ab und ein Survivor wählt das Profil auf seinem nächsten Zyklus (Self-Heal). Auf Graceful Shutdown gibt der Node seine Leases sofort frei, daher ist Failover schnell.
- **Watchdog** – ein Host, dessen Task exit ist, während das Profil immer noch unserer ist, wird neu gestartet.
- Reconcile ist jittered, um eine Thundering Herd von `UPDATE`s in skalierten Systemen zu vermeiden.

## Konsequenzen

- Kein eigenständiger Koordinator zum Bereitstellen oder am Leben halten – Postgres ist die einzige Quelle der Wahrheit.
- Double-Copying wird durch Row-Level-Atomarität verhindert, nicht durch Application-Level-Locking.
- Failover-Latenz ist durch die Lease TTL begrenzt (minus der Fast-Path Graceful Release).
- Dies ist der Money-Path; er wird durch die deterministische Stress-Suite (DST) bewacht – weaken nie ein DST-Szenario, um es zu bestehen.
