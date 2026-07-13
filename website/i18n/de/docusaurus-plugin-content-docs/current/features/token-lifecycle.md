---
description: "Die cTrader Open API erlaubt nur ein gültiges Access-Token pro cTrader-ID (cID) gleichzeitig. Sobald ein neues Token ausgestellt wird – eine geplante Aktualisierung oder eine erneute Autorisierung, wenn der Benutzer ein weiteres Konto derselben cID verknüpft – wird das vorherige Access-Token ungültig. Eine Copy-Engine, die auf einem Remote-Node läuft, hält dieses nun tote Token, weshalb das neue Token es erreichen muss, ohne die Live-Verbindung zu kappen."
---

# Open API Token-Lebenszyklus

Die cTrader Open API erlaubt **ein gültiges Access-Token pro cTrader-ID (cID) gleichzeitig**. Sobald ein
neues Token ausgestellt wird – eine geplante Aktualisierung oder eine erneute Autorisierung, wenn der
Benutzer ein weiteres Konto derselben cID verknüpft – wird das vorherige Access-Token ungültig. Eine
Copy-Engine, die auf einem Remote-Node läuft, hält dieses nun tote Token, weshalb das neue Token es
erreichen muss, ohne die Live-Verbindung zu kappen.

## Modell

- **`OpenApiAuthorization`** ist das Aggregate, das die verschlüsselten Access- und Refresh-Tokens einer
  cID hält. Ein eindeutiger Index auf `(UserId, CtidUserId)` erzwingt **genau eine Autorisierung pro cID
  pro Benutzer**.
- **`TokenVersion`** — ein monotoner Zähler, der bei jeder Token-Rotation erhöht wird (`Refresh()`,
  deckt auch den Re-Auth-Pfad ab, wenn ein weiteres Konto derselben cID verknüpft wird). Er ist der
  Versionsmarker für die Einzel-gültiges-Token-Regel und das, was ein laufender Host verwendet, um eine
  Änderung zu erkennen, selbst wenn zwei Token-Strings zufällig kollidieren.
- Tokens werden im Ruhezustand über `ISecretProtector` verschlüsselt
  (`EncryptionPurposes.OpenApiAccessToken` / `OpenApiRefreshToken`). Sie werden niemals im
  Klartext protokolliert oder gespeichert.

## Weitergabe (sanfter In-Place-Tausch)

1. Ein Token wird rotiert → das neue Token + erhöhte `TokenVersion` werden gespeichert.
2. Der `CopyEngineSupervisor` auf dem Hosting-Node liest den Plan in jedem Abgleichzyklus neu und
   berechnet eine **Token-Signatur** (Access-Tokens + Versionen). Eine Änderung bedeutet eine Rotation.
3. Anstatt den Host abzubauen und neu zu starten (was den Master-Ausführungsstream kappen würde),
   **pusht der Supervisor das neue Token zum laufenden Host**.
4. Der Host authentifiziert das betroffene Konto **auf dem bestehenden Socket** erneut
   (`ProtoOAAccountAuthReq` erneut) über `SwapAccessTokenAsync`, dann findet ein kurzer
   Abgleich statt. Das alte Token stirbt; der Copy-Stream hört nie auf.

Das macht den Fall über cIDs hinweg sicher: Ein Benutzer, der mitten im Betrieb ein zweites Konto
derselben cID hinzufügt, macht das alte Token ungültig, und das laufende Copy-Profil läuft mit dem
neuen weiter.

## Aktualisierung

`OpenApiTokenRefreshService` (Hintergrunddienst) aktualisiert proaktiv Autorisierungen vor Ablauf;
`OpenApiAuthorization.IsExpiring(threshold, now)` steuert dies. cTrader rotiert das
**Refresh**-Token bei jeder Aktualisierung, also wird das neue Refresh-Token sofort gespeichert; ein
schreibgeschützter Cache, der nicht persistieren kann, macht sich selbst ungültig (relevant für den
In-Cluster-Test-Job, der eine beschreibbare Kopie des Secrets einhängt).

### Failure-Escalation

Eine fehlgeschlagene Aktualisierung ist nicht still. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
speichert `RefreshFailedAt`, erhöht `ConsecutiveRefreshFailures` und löst immer ein
`AccessTokenRefreshFailed` (Warning) aus. Wenn das Token nun innerhalb von
`App:OpenApi:TokenRefreshCriticalWindow` (Standard 6h) vor Ablauf liegt und die Aktualisierung weiterhin
fehlschlägt, eskaliert es **einmalig** mit einem `AccessTokenRefreshCritical` Domain Event + `Critical`-Log,
sodass der Eigentümer die Autorisierung erneuern kann, bevor Copy/Prop-Firm-Operationen das Token
verlieren. Der Fehlerzähler und die Eskalations-Sperre werden beim nächsten erfolgreichen `Refresh`
zurückgesetzt. Der Dienst versucht es weiterhin alle `TokenRefreshInterval`, sodass ein
Provider-/Wartungsausfall sich selbst heilt, wenn der Refresh-Endpunkt zurückkehrt.

## Invalidierungs-Alert & Auto-Recovery (M1)

Eine Teil-/Wieder-Autorisierung auf einer cID macht das Token ungültig, das ein laufender Copy-Host noch
hält. Wenn ein Trading-Aufruf mit `OpenApiErrorKind.TokenInvalid` abgelehnt wird, löst der Host einen
eigenständigen **`CopyTokenInvalidated`**-Alert aus (Log 1078) — kein generischer Fehler — damit der
Benachrichtigungskanal weiß, dass ein Token Aufmerksamkeit erfordert. Die Recovery ist automatisch: Der
Supervisor liest die Autorisierung in jedem Zyklus neu, und wenn das aktualisierte Token die
Token-Signatur ändert, pusht er es in den laufenden Host für einen **In-Place-Tausch** — das Kopieren
wird ohne manuelles erneutes Hinzufügen fortgesetzt. Ein `NotLinkable`-Profil (Token/Auth vorübergehend
nicht auflösbar) wird ebenfalls in jedem Supervisor-Zyklus neu ausgewertet und gehostet, sobald sein Plan
wieder aufgebaut wird.

## Host-Liveness-Watchdog (M2)

Der Supervisor überwacht die Run-Task jedes gehosteten Profils. Wenn ein Host beendet wird oder einen
Fehler hat, während sein Profil noch diesem Node zugewiesen ist, bricht der Watchdog ab und
**startet ihn im nächsten Zyklus neu** (Log `CopyHostRestarted`), sodass ein festgefahrener Host sich
selbst heilt, anstatt einen manuellen Neustart zu benötigen — und das Scheitern eines Profils nie die
anderen blockiert (Per-Profil-Isolation).

## Tests

- **Unit** — `TokenVersion` erhöht sich bei `Refresh`; Host führt einen In-Place-Tausch ohne Neustart
  durch; cID-übergreifende Invalidierung tauscht Quell- und Ziel-Token; **ein invalidiertes
  Ziel-Token löst `CopyTokenInvalidated` aus und erholt sich automatisch beim nächsten Token-Push** (M1);
  die Watchdog-Entscheidung `IsHostDead` startet einen beendeten/fehlerhaften Host neu und lässt ein
  neu zugewiesenes Profil in Ruhe (M2).
- **Integration** — `TokenVersion` persistiert + erhöht sich über EF auf echtem Postgres; die
  Token-Signatur ändert sich bei einem Versions-Bump, selbst wenn der String unverändert bleibt.
