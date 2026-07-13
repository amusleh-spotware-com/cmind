---
description: "Livrer une application cTrader Open API pour chaque utilisateur (mode partagé white-label), l'URL de redirection unique pour s'enregistrer et les limites de débit de client par type de message."
---

# Application Open API partagée & limites de débit

Par défaut, chaque utilisateur enregistre sa **propre** application cTrader Open API sous **Paramètres → Open API**. Un opérateur white-label (généralement un courtier cTrader ou revendeur) peut à la place livrer **une application Open API partagée pour tous les utilisateurs** — personne ne s'enregistre lui-même ; tout le monde autorise ses comptes via l'application unique de l'opérateur.

## Deux façons de fournir l'application partagée

L'application partagée est fournie soit à partir de la configuration du déploiement, **soit** à partir de l'interface utilisateur des paramètres du propriétaire (la valeur définie par le propriétaire gagne). Fournissez-la une fois et le mode partagé s'active pour tout le monde.

### 1. Configuration du déploiement (amorcée au démarrage)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // URL publique canonique de CE déploiement
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // chiffré au repos ; jamais enregistré
    }
  }
}
```

Au démarrage, l'application amorce une application partagée détenue par le compte du propriétaire (idempotent — elle ne surcharge jamais une valeur runtime modifiée par propriétaire, et le re-seed est un no-op).

### 2. Paramètres du propriétaire (runtime, pas de redéploiement)

**Paramètres → Open API** (propriétaire uniquement) affiche une carte **Application partagée de déploiement** : ajouter / modifier / supprimer l'application partagée, avec l'URL de redirection affichée pour copier-coller. Les modifications prennent effet pour les nouvelles autorisations immédiatement.

## L'URL de redirection (enregistrez ceci dans cTrader)

Chaque application cTrader Open API enregistre **une** URL de redirection — la **même valeur unique** pour l'application partagée et pour toute application par utilisateur :

```
{votre URL de déploiement}/openapi/callback
```

par exemple `https://cmind.yourbroker.com/openapi/callback`.

- L'application **affiche la valeur exacte** sur la page des paramètres Open API (avec un bouton de copie) — collez-la dans le portail partenaire cTrader quand vous créez l'application Open API.
- Elle est composée à partir de `App:OpenApi:PublicBaseUrl` donc elle reste stable derrière un proxy inverse / CDN ; quand c'est indéfini, elle revient à l'hôte de requête entrant.
- L'expérience invite vs utilisateur normal ne diffère que par l'endroit où l'utilisateur atterrit **après** le rappel (liste de comptes vs confirmation "comptes ajoutés") — l'URL de redirection enregistrée est inchangée.

## Que voient les utilisateurs en mode partagé

Quand une application partagée existe :

- Les utilisateurs ne reçoivent **aucune option** pour enregistrer leur propre application Open API — la page des paramètres affiche **"Open API est géré par votre fournisseur"** et un bouton **Autoriser les comptes** qui utilise l'application partagée.
- Toute application personnelle pré-existante est **supprimée** ; ses comptes autorisés sont re-pointés vers l'application partagée et doivent être **re-autorisés** (leurs anciens jetons ont été émis sous un client-id différent). Tenter de créer une application personnelle retourne une erreur "géré par votre fournisseur".

## Limites de débit de client (par type de message)

Le client aligne les messages cTrader Open API sortants afin qu'une rafale ne déclenche jamais un blocage de limite de débit côté serveur. Les limites sont **par type de message**, correspondant aux docs cTrader Open API :

| Catégorie | Couverture | Par défaut |
|---|---|---|
| `General` | messages de commerce + lecture (ordres, symboles, requêtes de compte) | 45 msg/s |
| `HistoricalData` | requêtes de trendbar / données de tick (plus limites par cTrader) | 5 msg/s |

Une requête de données historiques compte contre **à la fois** son propre seau et le seau général. Les messages de battement de cœur et authentification ne sont jamais espacés. Les messages mettent en file d'attente et se vident au débit disponible — rien n'est déposé et l'ordre est préservé.

Réglez-les si votre courtier a négocié les limites cTrader **supérieures**, ou définissez une catégorie sur **`0`** pour désactiver complètement l'espacement (illimité) :

- **Config :** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (messages/sec).
- **Paramètres du propriétaire :** la carte **Limites de débit client** sur **Paramètres → Open API** (le remplacement du propriétaire gagne, s'applique aux nouvelles connexions / sur reconnexion).
