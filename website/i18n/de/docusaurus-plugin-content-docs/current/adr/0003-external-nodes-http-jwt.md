---
title: 0003 – cTrader CLI Nodes sind HTTP + JWT, keine SSH/Shell
description: Warum Remote-Node-Agents nur eine HTTP API mit kurzlebigen JWTs exposieren und niemals eine Shell.
---

# 0003 – cTrader CLI Nodes sind HTTP + JWT, keine SSH/Shell

## Kontext

Backtest/Run-Container führen auf Remote-Hosts aus. Der offensichtliche Ansatz – SSH ein und docker laufen – gibt der Haupt-App willkürliche Remote-Code-Ausführung und langlebige Anmeldedaten auf jedem Node. Das ist ein großer Blast-Radius für ein System, das untrusted Benutzer-cBots ausführt.

## Entscheidung

Jeder Remote-Host führt einen eigenständigen `CtraderCliNode` **HTTP-Agent** mit **keine SSH und keine Shell** aus. Die Haupt-App ruft den Agent über HTTP auf; jede Anfrage trägt ein kurzlebiges **HS256 JWT** (5 Minuten, `iss=app-main` / `aud=app-node`), das mit dem Node-Secret des Agenten signiert ist. Der Agent:

- führt nur Images aus, die `AllowedImagePrefix` entsprechen (mit einer Pfad-Grenze, daher `ghcr.io/spotware` kann nicht `ghcr.io/spotware-evil/...` entsprechen);
- execs docker über `ArgumentList` – niemals ein Shell-String;
- ist **zustandslos**, findet Container über das `app.instance`-Label;
- registriert sich selbst an und macht Heartbeat zu `POST /api/nodes/register`; die Haupt-App upsert den `CtraderCliNode` **nach Name**, daher überlebt ein Node IP-Änderungen.

## Konsequenzen

- Ein Lecks-Request-Token läuft in Minuten ab; es gibt keine stehende Shell-Anmeldedaten zu stehlen.
- Die Agent-Fähigkeit ist begrenzt auf "führe ein erlaubtes Image aus" – es kann nicht in eine allgemeine Remote-Shell umgewandelt werden.
- Node-Identität ist name-basiert, daher Re-Provisioning eines Nodes mit einer neuen IP verlässt seine History nicht verwaist.
