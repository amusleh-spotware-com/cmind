---
description: "Dies ist eine Handels-/Finanz-App: die Datenbank hält Handelskonten, Copy-Profile, Prop-Firm-Herausforderungen, Audit-Ketten und den Data Protection Schlüsselring…"
---

# Sicherung & Notfallwiederherstellung

Dies ist eine Handels-/Finanz-App: die Datenbank hält Handelskonten, Copy-Profile, Prop-Firm-Herausforderungen, Audit-Ketten und den Data Protection Schlüsselring. Sie zu verlieren verliert Geld und bricht Regelungs-/Audit-Verpflichtungen. Sichern Sie sie, und **beweisen Sie, dass die Wiederherstellung funktioniert**.

## Ziele

| Metrik | Ziel | Bedeutung |
|--------|--------|---------|
| RPO (Max Datenverlust) | ≤ 5 min | Verwenden Sie Zeitpunkt-Wiederherstellung (durchgehend WAL), nicht nur nächtliche Dumps. |
| RTO (Max Ausfallzeit) | ≤ 1 h | Zeit zu Wiederherstellung + Umleitung der App an die wiederhergestellte Datenbank. |
| Sicherungs-Aufbewahrung | ≥ 35 Tage | Deckt eine spät entdeckte Beschädigung + monatliche Audit-Fenster ab. |
| Wiederherstellungs-Übung | monatlich | Eine ungetestete Sicherung ist keine Sicherung. |

## Was muss gesichert werden

1. **Die Postgres-Datenbank** — alle App-Daten (einzelne logische Datenbank `appdb`).
2. **Der Data Protection Schlüsselring** — persistiert **in** der Datenbank (`PersistKeysToDbContext<DataContext>`) und PFX-verschlüsselt via `App:DataProtectionCertBase64`. Es folgt der DB-Sicherung, **aber das schützende Zertifikat + sein Passwort (`App:DataProtectionCertPassword`) sind Geheimnisse, die außerhalb der DB gespeichert werden** — sichern Sie sie in Ihrem Geheimnisse-Manager. Ohne das Zertifikat können Sie Geheimnisse (cTID-Passwörter, Open API Token, Knoten-Geheimnisse, KI-Schlüssel) nach einer Wiederherstellung nicht entschlüsseln.

## Managed Postgres (empfohlen)

Beide Cloud IaC Pfade stellen managed Postgres mit eingebauter PITR bereit — aktivieren + überprüfen Sie Aufbewahrung:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): set `backup.backupRetentionDays` (≥ 35) und `geoRedundantBackup` wo Compliance es erfordert. Wiederherstellung mit *Point-in-time Restore* zu einem neuen Server, dann aktualisieren Sie die App `appdb` Verbindungszeichenfolge.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): set `backup_retention_period` (≥ 35) und `backup_window`; halten Sie automatisierte Sicherungen + optional Cross-Region Kopie. Wiederherstellung mit *RestoreDBInstanceToPointInTime*, dann Umleitung der App.

Managed PITR gibt die ≤ 5 min RPO ohne App-Änderungen — die App braucht nur die neue Verbindungszeichenfolge (und die vorhandene Wiederholungs-Ausführungs-Strategie, siehe [scaling.md](../deployment/scaling.md), toleriert den Umschalter Flip).

## Self-hosted Postgres

- **Durchgehend Archivierung (PITR):** aktivieren Sie WAL-Archivierung (`archive_mode=on`, `archive_command` zu Objekt-Speicher) + ein periodisches `pg_basebackup`. Wiederherstellung = Basis-Sicherung wiederherstellen + WAL zur Zielzeit abspielen. Dies ist was die RPO-Ziel erfüllt.
- **Logische Dumps (sekundär):** nächtliches `pg_dump -Fc appdb` zu Off-Box-Speicher für Portabilität / Teilweise Wiederherstellungen. Nicht ausreichend allein für die RPO-Ziel.
- Verschlüsseln Sie Sicherungen im Ruhestatus; speichern Sie außerhalb des Datenbank-Hosts.

## Wiederherstellungs-Übung (täglich ausführen)

1. Stellen Sie die neueste Sicherung (PITR zu "jetzt − 10 min") in einer **Scratch**-Datenbank wieder her, nicht Produktion.
2. Verweisen Sie eine Einweg-App-Instanz (oder eine psql-Sitzung) darauf.
3. Überprüfen Sie Schema: `dotnet ef migrations list` zeigt keine ausstehenden Migrationen, App startet und wird `/health`-bereit.
4. **Überprüfen Sie die Audit-Kette** ist intakt und ungebrochen via `IAuditTrailVerifier` (die Manipulationssichere `AuditChainInterceptor` Kette) — eine unterbrochene Kette nach Wiederherstellung bedeutet Beschädigung oder Manipulation.
5. Bestätigen Sie, dass die Geheimnisse-Entschlüsselung funktioniert (z. B. eine Open API-Autorisation entschlüsselt) — beweist die Data Protection Zertifikat + Passwort wurden korrekt wiederhergestellt.
6. Zeichnen Sie das Übungs-Ergebnis auf (Zeit genommen vs RTO) und zerstören Sie die Scratch-Datenbank.

Automatisieren Sie Schritte 1–4 in CI wo die Umgebung erlaubt (Stellen Sie eine Seeded-Sicherung in einen Testcontainer wieder her, laufen Sie `dotnet ef migrations list` + die Audit-Ketten-Überprüfung) daher wird eine unterbrochene-Sicherungs-Regressions vor Ihnen es brauchen gefangen.

## Nach einer echten Wiederherstellung

1. DB wiederherstellen (PITR zu gerade vor dem Zwischenfall).
2. Stellen Sie sicher, dass die Data Protection Zertifikat + Passwort die **gleichen** sind, die vor dem Zwischenfall verwendet werden.
3. Umleitung der App `appdb` Verbindungszeichenfolge; Rollte die Replikate.
4. Startup läuft Migrationen unter der Advisory Lock (siehe scaling.md) — sicher mit N Replikaten.
5. Copy/Prop-Firm Supervisor fordern ihre Ansprüche zurück und **Resync vom Broker** (cTrader ist die Quelle der Wahrheit), daher konvergieren sich offene Positionen automatisch — nichts wird aus einem stale lokalen Status vertraut.
6. Überprüfen Sie Audit-Kette + Spot-Check der letzten Handels-Daten.
