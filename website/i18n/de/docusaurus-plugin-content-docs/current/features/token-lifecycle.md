---
description: "cTrader Open API erlaubt ein gültiger Zugriff-Token pro cTrader ID (cID) auf einmal. Der Moment ein neuer Token ausgestellt wird — eine geplante Aktualisierung oder ein…"
---

# Open API Token Lebenszyklus

cTrader Open API erlaubt **ein gültiger Zugriff-Token pro cTrader ID (cID) auf einmal**. Der Moment ein neuer Token ausgestellt wird — eine geplante Aktualisierung oder eine Re-Authorisierung wenn der Benutzer ein anderes Konto auf dem gleichen cID verlinkt — der vorherige Zugriff-Token wird ungültig gemacht. Eine Copy-Engine, die auf einem Remote-Knoten läuft, hält diesen nun-toten Token, daher muss der neue Token es erreichen ohne die Live-Verbindung fallen zu lassen.

## Modell

- **`OpenApiAuthorization`** ist das Aggregate, das einen cID-Zugriff + Aktualisierungs-Token verschlüsselt hält. Ein eindeutiger Index auf `(UserId, CtidUserId)` erzwingt **exakt eine Autorisierung pro cID pro Benutzer**.
- **`TokenVersion`** — ein monoton Zähler bumped jedes Mal der Token rotiert (`Refresh()`, die auch die Re-Auth Pfad abdeckt wenn ein anderes Konto auf dem gleichen cID verlinkt wird). Es ist der Version-Marker für die Einzel-gültig-Token-Regel und ist was ein laufender Host verwendet um eine Änderung zu erkennen, selbst wenn zwei Token-Zeichenfolgen zufällig kollidieren.
- Token werden im Ruhestatus über `ISecretProtector` verschlüsselt (`EncryptionPurposes.OpenApiAccessToken` / `OpenApiRefreshToken`). Sie werden nie protokolliert oder in Klartext gespeichert.

## Ausbreitung (Anmutige In-Place-Tausch)

1. Ein Token rotiert → der neue Token + Bumped `TokenVersion` werden persistiert.
2. Der `CopyEngineSupervisor` auf dem Hosting-Knoten re-liest den Plan jedes Abstimmungs-Zyklus und berechnet ein **Token-Signatur** (Zugriff-Token + Versionen). Eine Änderung bedeutet eine Rotation.
3. Anstatt den Host abzureißen und neu zu starten (was würde fallen den Master Ausführungs-Strom fallen lassen), der Supervisor **drückt den neuen Token zum laufenden Host**.
4. Der Host re-authentifiziert das betroffene Konto **auf dem vorhandenen Socket** (`ProtoOAAccountAuthReq` erneut) via `SwapAccessTokenAsync`, dann tut eine leichte Abstimmung. Der alte Token stirbt; der Copy-Strom niemals stoppt.

Das ist, was die Cross-cID Fall sicher macht: ein Benutzer addiert ein zweite Konto vom gleichen cID Mid-Run ungültig gemacht den alten Token, und die laufende Copy-Profil geht weiter auf dem neuen.

## Aktualisierung

`OpenApiTokenRefreshService` (Hintergrund) proaktiv erneuert Autorisierungen vor dem Ablauf; `OpenApiAuthorization.IsExpiring(Schwelle, Jetzt)` Tore es. cTrader rotiert den **Aktualisierungs**-Token auf jeden Aktualisierung, daher der neue Aktualisierungs-Token wird sofort persistiert; ein Lese-nur Cache, der nicht persistieren können würde sich selbst-ungültig machen (relevant zum in-Cluster Test Job, das ein beschreibbar Kopie des Geheimnisses montiert).

### Misserfolg Eskalation

Ein fehlgeschlagener Aktualisierung ist nicht stumm. `OpenApiAuthorization.MarkRefreshFailed(Grund, Jetzt, CriticalWindow)` Datensätze `RefreshFailedAt`, Inkremente `ConsecutiveRefreshFailures`, und hebt immer `AccessTokenRefreshFailed` (Warnung). Wenn der Token ist nun innerhalb `App:OpenApi:TokenRefreshCriticalWindow` (Standard 6h) von Ablauf und Aktualisierung ist noch Fehler, es eskaliert **einmal** mit ein `AccessTokenRefreshCritical` Domänen-Ereignis + `Critical` Log daher Besitzer kann Re-Autorisierung vor Copy/Prop-Firm Operationen den Token verlieren. Der Fehler-Zähler und Eskalations-Verriegelung Rücksetze auf der nächsten erfolgreich `Refresh`. Der Service trägt Wiederholung weiter alle `TokenRefreshInterval`, daher ein Provider/Wartungs Ausfalls selbst-heilt wenn der Aktualisierungs-Endpoint gibt zurück.

## Ungültig-Warnung & Auto-Wiederherstellung (M1)

Ein Teils/erneute Autorisierung auf ein cID ungültig gemacht den Token ein laufender Copy-Host noch hält. Wenn ein Handels-Anruf lehnt mit `OpenApiErrorKind.TokenInvalid`, der Host hebt ein eigenständiger **`CopyTokenInvalidated`** Warnung (Log 1078) — nicht ein generisches Misserfolg — daher der Benachrichtigungs-Kanal weiß ein Token braucht Aufmerksamkeit. Wiederherstellung ist Automatisch: der Supervisor re-liest die Autorisierung jedes Zyklus und, wenn der erneuert Token Änderungen die Token-Signatur, drückt es in den laufenden Host für ein **In-Place-Tausch** — Kopieren setzt fort mit nein manuell Re-Hinzufügen. Ein `NotLinkable` Profil (Token/Auth zeitweise nicht-lösbar) ist gleich neu-evaluiert alle Supervisor Zyklus und gehostet der Moment sein Plan Baut erneut.

## Host Lebensfähigkeit Watchdog (M2)

Der Supervisor überwacht jede gehostete Profil läuft Task. Wenn ein Host beendet oder Fehler während sein Profil ist noch diesem Knoten zugeordnet, der Watchdog storniert und **neu Starts** es nächst Zyklus (Log `CopyHostRestarted`), daher ein keiliger Host selbst-heilt anstatt brauche ein manuell Neustart — und ein Profil Misserfolg nie stellt die andere (pro-Profil Isolation).

## Tests

- **Unit** — `TokenVersion` Bumps auf `Refresh`; Host führt ein In-Place-Tausch ohne Neustart; Cross-cID Ungültigung tauscht Quelle und Ziel Token; **ein ungültig gemacht Ziel Token hebt `CopyTokenInvalidated` und Auto-stellt wieder auf der nächsten Token Drücken** (M1); der Watchdog `IsHostDead` Entscheidung startet ein abgeschlossen/fehlgeschlagen Host und blatt ein neu-zugeordnet Profil allein (M2).
- **Integration** — `TokenVersion` persistiert + inkremente durch EF auf real Postgres; der Token-Signatur Änderungen auf ein Version Bump, selbst wenn die Zeichenfolge ist unverändert.
