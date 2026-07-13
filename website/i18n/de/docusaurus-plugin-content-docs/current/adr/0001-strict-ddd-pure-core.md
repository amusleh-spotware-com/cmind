---
title: 0001 – Striktes DDD mit reinem Core
description: Warum Geschäftslogik auf Aggregaten in einem Core-Projekt mit null Infrastruktur-Abhängigkeiten lebt.
---

# 0001 – Striktes DDD mit reinem `Core`

## Kontext

Diese App bewegt echtes Geld. Geschäftsregeln, die über Endpoints, Background Services und Razor-Komponenten verstreut sind, verrotten zu untestbaren, inkonsistenten Verhaltensweisen – genau dort, wo ein Bug einen Benutzer Kapital kostet.

## Entscheidung

Geschäftslogik lebt **auf Aggregaten, Value Objects und Domain Services** in `src/Core`, die mit **null Infrastruktur-Abhängigkeiten** kompiliert (kein EF, HttpClient, Docker oder ASP.NET). Endpoints, MCP-Tools, Komponenten und `BackgroundService`s **orchestrieren** – sie entscheiden nie. Regeln:

- Keine öffentlichen Setter; State-Änderungen durch intention-revealing Methoden, die Invarianten schützen.
- Aggregate referenzieren sich gegenseitig über **Strong ID**, nicht Navigationseigenschaft.
- Ein `SaveChanges` mutiert **ein** Aggregat; Cross-Aggregat-Flüsse verwenden Domain Events.
- Primitives, die eine Domain-Grenze queren, werden in Value Objects umgewickelt.
- Invarianten-Verletzungen werfen eine Core `DomainException`, nicht eine Framework-Exception.

## Konsequenzen

- Domain-Regeln sind Unit-testbar ohne Datenbank oder Web-Host.
- `Core` Reinheit wird maschinell durchgesetzt durch `ArchitectureGuardTests` und würde den Build fehlschlagen lassen, wenn kaputt.
- Es gibt mehr Zeremonie (Value Objects, Strong IDs, Domain Events) als ein anämisches Modell – dies ist die absichtliche Kosten, um die Geld-bewegenden Regeln korrekt und an einem Ort zu halten.
