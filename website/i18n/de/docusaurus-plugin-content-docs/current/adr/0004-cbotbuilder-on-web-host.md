---
title: 0004 – CBotBuilder läuft auf dem Web-Host in einem Sandbox-Container
description: Warum untrusted cBot-Builds auf dem Web-Host in einem einmaligen SDK-Container stattfinden, anstatt auf einem Node.
---

# 0004 – `CBotBuilder` läuft auf dem Web-Host in einem Sandbox-Container

## Kontext

Das Bauen eines Benutzer-cBots bedeutet, **untrusted MSBuild** auszuführen – beliebiger Code zur Build-Zeit (Targets, Source Generators, Restore-Scripts). Es benötigt den Docker-Socket, um einen SDK-Container zu spinnen. Nodes führen Trading-Container aus und sollten auch keine Build-Privilegien halten.

## Entscheidung

`CBotBuilder` läuft **auf dem Web-Host** (der bereits den Docker-Socket hat), innerhalb eines **einmaligen SDK-Containers** mit:

- ein Bind-gemountetes `/work`-Verzeichnis (nur die Build-Eingaben/Ausgaben, nicht das Host-Dateisystem);
- ein gemeinsames `app-nuget-cache`-Volume für Restore-Performance;
- kein Host-Netzwerk-Zugriff über das hinaus, was Restore braucht.

Daher kann untrusted MSBuild nicht das Host-Dateisystem oder Netzwerk erreichen. Run/Backtest-Container laufen hingegen auf Nodes, die von `NodeScheduler` gewählt werden.

## Konsequenzen

- Build-Privileg (Docker-Socket) ist auf den Web-Host begrenzt; Nodes führen nur erlaubte Trading-Images aus.
- Jeder Build ist in einem einmaligen Container isoliert – ein malicious Build kann nicht persistieren oder entkommen.
- Der Web-Host muss einen Docker-Socket verfügbar haben; dies ist eine Bereitstellungs-Anforderung, nicht optional.
