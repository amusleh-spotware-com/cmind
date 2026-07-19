# API cBot COT

Les données d'Engagement des Commerçants sont exposées aux cBots et aux clients externes via une API REST authentifiée, de sorte qu'une stratégie puisse extraire le positionnement (position nette, % d'intérêt ouvert, indice COT) comme entrée de signal. Elle réutilise la **même mécanique JWT et portée `market:read`** que l'API de marché de force des devises — un jeton, un schéma.

## Authentification

1. Dans l'application, émettez un client d'API de données de marché (propriétaire) et accordez-lui la portée **`market:read`**.
2. Échangez l'id/secret du client pour un jeton bearer de courte durée :

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   La réponse contient `token`, `expiresAt` et les `scopes` accordés.
3. Envoyez le jeton à chaque appel COT :

   ```http
   Authorization: Bearer <token>
   ```

Un jeton manquant/invalide retourne `401` ; un jeton sans `market:read` retourne `403`.

## Points de terminaison

Chemin de base `/api/market/v1/cot`. Toutes les réponses sont JSON.

| Méthode et chemin | Objectif |
|---------------|---------|
| `GET /markets` | Le catalogue de marchés-contrats suivis. `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) et mot-clé `q` optionnels. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | Le dernier instantané hebdomadaire pour un marché. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Historique hebdomadaire sur une fenêtre. |

Paramètres :

- `code` — le code de marché de contrat CFTC (par exemple `099741` pour Euro FX ; obtenez-le à partir de `/markets`).
- `kind` — `Legacy` (défaut), `Disaggregated` ou `Tff`.
- `combined` — `true` pour contrats + options, `false` (défaut) pour contrats uniquement.
- `asOf` (ISO-8601, optionnel) — ancre ponctuelle : seuls les rapports publics à cet instant sont retournés, de sorte qu'un backtest ne voit aucune anticipation.

### Exemple

```http
GET /api/market/v1/cot/latest?code=088691&kind=Legacy HTTP/1.1
Authorization: Bearer <token>
```

```json
{
  "contractCode": "088691",
  "marketName": "Gold",
  "kind": "Legacy",
  "combined": false,
  "reportDate": "2024-01-02T00:00:00+00:00",
  "knownAt": "2024-01-05T20:30:00+00:00",
  "openInterest": 450000,
  "cotIndex": 82.4,
  "extreme": "LongExtreme",
  "categories": [
    { "category": "NonCommercial", "long": 250000, "short": 90000, "net": 160000, "longPercentOfOi": 55.5 }
  ]
}
```

## Outils MCP

Le même modèle de lecture est disponible pour les clients IA en tant qu'outils MCP : `CotMarkets`, `CotLatest`, `CotHistory` et `CotHealth` — chacun étant correct ponctuellement via un `asOf` optionnel. Consultez la [fonctionnalité Engagement des Commerçants](./cot-report.md) pour l'image complète.

## Portail

L'API est derrière la même porte à deux niveaux que la page : `App:Branding:EnableCot` et `App:Features:Cot`. Avec l'une d'elles désactivée, chaque route sous `/api/market/v1/cot` retourne `404`.
