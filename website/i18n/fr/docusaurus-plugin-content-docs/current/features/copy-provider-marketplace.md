---
description: "Répertoire consultable de stratégies de copie. Le fournisseur publie le profil de copie en tant que liste avec badge verified-live (le compte source de stratégie échange de l'argent réel, pas…"
---

# Place de marché des fournisseurs de copie (Phase 4)

Répertoire consultable de stratégies de copie. Le fournisseur **publie** le profil de copie en tant que liste avec badge **verified-live** (le compte source de stratégie échange de l'argent réel, pas démo) plus les frais de performance. Les followers parcourent la place de marché, classés par score de performance projeté à partir des données de transparence d'exécution.

## Modèle

- `CopyProviderListing` = agrégat : `UserId`, `ProfileId`, nom d'affichage, description, frais de performance, `VerifiedLive`, `Published` + `PublishedAt`. Une liste par profil (index unique).
- **Verified-live** dérivé au moment de la publication à partir du `TradingAccount.IsLive` source du profil — le fournisseur ne peut pas auto-affirmer.
- Les statistiques de performance **non stockées sur la liste** — projection de modèle de lecture sur le journal de transparence `CopyExecution` (taux de remplissage, latence moyenne, glissement réalisé moyen), donc la place de marché reflète toujours la qualité d'exécution en direct.

## Classement

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → score 0–100 : le taux de remplissage domine (×60), la latence faible + le glissement faible ajoutent (×20 chacun), le badge verified-live ajoute un petit bonus de confiance. Déterministe + monotone, donc l'ordre stable.

## API

- `POST /api/copy/profiles/{id}/publish` — publier/mettre à jour la liste des profils (`DisplayName`, `Description`, `PerformanceFeePercent`) ; verified-live défini à partir du compte source.
- `DELETE /api/copy/profiles/{id}/publish` — dépublier.
- `GET /api/copy/marketplace` — toutes les listes publiées, classées, chacune avec résumé de performance (exécutions, taux de remplissage, latence moyenne, glissement moyen, score) + badge verified-live.

## Tests

- **Unit** (`CopyProviderListingTests`) — invariants d'agrégat : le nom d'affichage requis ; publier définit l'horodatage ; dépublier masquer ; la mise à jour remplace les champs d'affichage + frais + badge.
- **Integration** (`CopyMarketplaceTests`, Postgres réel) — la liste publiée persiste avec le badge ; une liste par profil (index unique) ; le score de classement préfère les fournisseurs vérifiés/remplissage élevé.

L'hôte de copie n'est pas touché (listes + modèle de lecture uniquement), donc la suite de stress DST de copie n'est pas affectée.
