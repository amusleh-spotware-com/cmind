---
description: "C'est une app de trading/finance : la BD tient les comptes de trading, profils de copie, défis prop-firm, chaînes d'audit, et l'anneau de clés Data Protection…"
---

# Sauvegarde & récupération après sinistre

C'est une app de trading/finance : la BD tient les comptes de trading, profils de copie, défis prop-firm,
chaînes d'audit, et l'anneau de clés Data Protection. La perdre perd de l'argent et casse les obligations
réglementaires/d'audit. Sauvegardez-la, et **prouvez que la restauration fonctionne**.

## Objectifs

| Métrique | Objectif | Signification |
|--------|--------|---------|
| RPO (perte de données max) | ≤ 5 min | Utilisez la récupération point-dans-le-temps (WAL continu), pas seulement les dumps nocturnes. |
| RTO (indisponibilité max) | ≤ 1 h | Temps pour restaurer + repointer l'app vers la BD restaurée. |
| Rétention de sauvegarde | ≥ 35 jours | Couvre une corruption découverte tard + fenêtres d'audit mensuelles. |
| Test de restauration | mensuel | Une sauvegarde non testée n'est pas une sauvegarde. |

## Ce qui doit être sauvegardé

1. **La BD Postgres** — toutes les données d'app (BD logique unique `appdb`).
2. **L'anneau de clés Data Protection** — persisté **dans** la BD
   (`PersistKeysToDbContext<DataContext>`) et PFX-chiffré via `App:DataProtectionCertBase64`.
   Il se déplace avec la sauvegarde BD, **mais le certificat de protection + son mot de passe
   (`App:DataProtectionCertPassword`) sont des secrets stockés hors de la BD** — sauvegardez-les dans votre
   gestionnaire de secrets. Sans le certificat, vous ne pouvez pas décrypter les secrets (mots de passe cTID, tokens Open API,
   secrets de nœud, clé IA) après une restauration.

## Postgres géré (recommandé)

Les deux chemins Cloud IaC provisionent Postgres géré avec PITR intégré — activez + vérifiez la rétention :

- **Azure** (`deploy/azure/main.bicep`, Flexible Server) : définissez
  `backup.backupRetentionDays` (≥ 35) et `geoRedundantBackup` si la conformité l'exige. Restaurez
  avec *Point-in-time restore* vers un nouveau serveur, puis mettez à jour la chaîne de connexion `appdb` de l'app.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform) : définissez `backup_retention_period` (≥ 35) et
  `backup_window` ; conservez les sauvegardes automatisées + copie cross-région optionnelle. Restaurez avec
  *RestoreDBInstanceToPointInTime*, puis reconfigurez l'app.

Le PITR géré donne le RPO ≤ 5 min sans changements d'app — l'app a juste besoin de la nouvelle chaîne de connexion
(et la stratégie d'exécution de retry existante, voir [scaling.md](../deployment/scaling.md), tolère le
coupure).

## Postgres auto-hébergé

- **Archivage continu (PITR) :** activez l'archivage WAL (`archive_mode=on`, `archive_command` vers
  le stockage objet) + une `pg_basebackup` périodique. Restauration = restaurer la sauvegarde de base + rejouer WAL jusqu'à l'
  heure cible. C'est ce qui rencontre l'objectif RPO.
- **Dumps logiques (secondaire) :** `pg_dump -Fc appdb` nocturne vers le stockage hors-box pour la portabilité /
  restaurations partielles. Pas suffisant seul pour l'objectif RPO.
- Chiffrez les sauvegardes au repos ; stockez-les hors de l'hôte de la BD.

## Test de restauration (mensuel)

1. Restaurez la dernière sauvegarde (PITR à « maintenant − 10 min ») dans une BD **temporaire**, pas production.
2. Pointez une instance d'app jetable (ou une session psql) vers elle.
3. Vérifiez le schéma : `dotnet ef migrations list` n'affiche pas de migrations en attente, l'app démarre et devient
   `/health`-prête.
4. **Vérifiez la chaîne d'audit** est intacte et ininterrompue via `IAuditTrailVerifier` (la chaîne
   `AuditChainInterceptor` infalsifiable) — une chaîne brisée après restauration signifie corruption ou falsification.
5. Confirmez que la décryption de secret fonctionne (par ex. un autorisation Open API décrypte) — prouve que le certificat Data
   Protection + le mot de passe ont été restaurés correctement.
6. Enregistrez le résultat du test (temps pris vs RTO) et détruisez la BD temporaire.

Automatisez les étapes 1–4 en CI où l'environnement le permet (restaurez une sauvegarde ensemencée dans un Testcontainer,
exécutez `dotnet ef migrations list` + vérification de chaîne d'audit) de sorte qu'une régression de sauvegarde cassée est détectée
avant que vous en ayez besoin.

## Après une vraie restauration

1. Restaurez la BD (PITR juste avant l'incident).
2. Assurez-vous que le certificat Data Protection + le mot de passe sont les **mêmes** en utilisation avant l'incident.
3. Reconfigurez la chaîne de connexion `appdb` de l'app ; redéployez les répliques.
4. Le démarrage exécute les migrations sous le verrou consultatif (voir scaling.md) — sûr avec N répliques.
5. Les superviseurs de copie/prop-firm recueillent leurs baux et **se resynchronisent à partir du courtier** (cTrader est la
   source de vérité), de sorte que les positions ouvertes se reconvergent automatiquement — rien n'est fiable à partir de
   l'état local obsolète.
6. Vérifiez la chaîne d'audit + vérifiez spot les données de trading récentes.
