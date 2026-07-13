# Force monétaire IA macro et perspective forward

cMind livre un moteur macro de force monétaire **assisté par IA, mathématiquement déterministe**. Il classe un
univers configurable de devises — les 8 majeurs plus les devises des marchés émergents et exotiques — par
**force** fondamentale **actuelle**, et projette une **perspective directionnelle forward** pour chaque paire sur
un horizon choisi (1M / 3M / 6M / 12M). Chaque classement, chaque biais de paire et chaque chiffre sont calculés
par des maths déterministes pures dans le cœur domaine ; le LLM se contente de *collecter* les entrées
forward-looking que les données ne peuvent pas publier et *explique* le résultat en anglais plain. Il n'invente
jamais un classement, une direction ou un chiffre.

> **Limitation honnête.** Les fondamentaux prédisent bien la valeur moyen-à-long terme et mal la valeur
> à court terme. Traitez ceci comme un filtre de positionnement / de confluence, **pas** un signal de timing
> à court terme. Les lectures près des publications à fort impact (NFP/CPI/banque centrale) sont bruitées.
> Pas un conseil financier.

## Comment ça fonctionne

1. **Les fondamentaux actuels viennent du calendrier économique, pas du LLM.** Les chiffres durs — taux
   directeurs, CPI vs objectif, PIB, emploi, balance commerciale — et leurs **z-scores de surprise** sont
   sourcés **point-in-time** depuis le module [calendrier économique](./economic-calendar.md) (FRED/BLS/BEA/ECB
   et calendriers des banques centrales). Un snapshot historique ne fuit jamais d'anticipation.
2. **Le LLM ne collecte que ce que le calendrier ne peut pas publier** — par devise : la trajectoire **forward**
   (chemin attendu du taux directeur en pb, tendance inflation vs objectif, élan de croissance) et une
   perspective **géopolitique** (risk-on/off, tarifs, budgétaire/dette, élections), plus toute donnée EM/exotique
   actuelle que le calendrier ne possède pas. JSON strict, validation tier-aware, recherche web activée.
3. **Le domaine calcule le classement et la matrice forward déterministiquement.** Chaque driver est noté comme un
   **z-score within-tier** (ainsi une inflation à 50% pour un exotic ne déforme jamais les majeurs), winsorisé,
   sommé pondéré en score composite, et classé strongest→weakest avec un tie-break ISO stable. La couche forward
   porte chaque composite le long de sa trajectoire —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — et mappe le différentiel projeté de chaque paire
   vers un **biais directionnel** (▲ appreciate / ▬ neutral / ▼ depreciate) avec une conviction.
4. **Le LLM explique** le classement et les principaux appels de paires en langage plain.

## Les drivers

| Driver | Effet sur la force | Notes |
|---|---|---|
| Taux directeur & trajectoire | Plus haut / hawkish ⇒ plus forte | Poids le plus élevé ; la divergence des banques centrales génère les plus écarts. |
| Inflation (CPI vs objectif) | Au-dessus de l'objectif ⇒ plus faible | Noté inversement (frein pouvoir d'achat). |
| Croissance PIB | Croissance relative plus élevée ⇒ plus forte | Différentiel vs le panel. |
| Emploi | Travail plus fort ⇒ plus forte | Alimente le chemin politique. |
| Balance commerciale / compte courant | Excédent ⇒ plus forte | Demande structurelle. |
| Orientation politique | Hawkish ⇒ plus forte | Le driver primaire long terme. |
| Élan de surprise | Révisions récentes positives ⇒ plus forte | Des z-scores de surprise du calendrier. |
| Géopolitique / risque | Risk-off ⇒ valeurs refuges (USD/JPY/CHF) plus fortes | Delta de risque forward borné. |
| Rendement réel / carry *(EM/exotic)* | Taux réel positif ⇒ plus forte | Driver EM dominant en régimes calmes. |
| Vulnérabilité externe *(EM/exotic)* | Déficits / faibles réserves / dette USD ⇒ plus faible | Pression structurelle de dépréciation. |
| Termes de l'échange *(exportateurs de commodities)* | Prix des export上涨 ⇒ plus forte | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Risque politique / institutionnel *(EM/exotic)* | Instabilité ⇒ plus faible | Bande morte plus large, conviction plafonnée. |

## Univers à trois niveaux (majeurs + EM + exotiques)

L'univers est **configurable au déploiement** (`App:CurrencyStrength:Universe`) — ajouter une devise est
de la config, pas du code. Chaque devise porte un **niveau** (`Major` / `EmergingMarket` / `Exotic`) qui
ajuste la pondération, la largeur de la bande morte et le plafond de conviction :

- **Majeurs** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (pilotés par le niveau des taux).
- **Marchés émergents** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK) ; carry + risque +
  vulnérabilité externe pondérés vers le haut, confiance moyenne.
- **Exotiques** — TRY, HUF, CZK, plus HKD/SAR adossés au USD ; confiance basse, bande morte large,
  conviction plafonnée. Les devises **adossées / fortement gérées** (HKD, SAR, CNH) sont signalées, leur
  trajectoire est down-weightée, et leur perspective de paire est ancrée vers `Neutral` ainsi un peg n'est
  jamais lu comme un signal free-floating.

Parce que les stats EM/exotiques officielles sont de fréquence plus basse, révisées et parfois opaques, les
chiffres collectés par IA portent une **confiance par niveau** affichée comme badge de fiabilité.

## Dégradation gracieuse

| Calendrier | IA | Résultat |
|---|---|---|
| ✅ | ✅ | Classement complet + projection forward + récit (`CalendarAndAi`). |
| ✅ | ❌ | Classement actuel calendrier seul, pas de projection forward (`CalendarOnly`). |
| ❌ | ✅ | Chiffres actuels collectés par IA + forward, confiance réduite (`AiOnly`). |
| ❌ | ❌ | Aucun snapshot — le widget se cache et la page montre un état vide. |

L'application fonctionne sans modification dans les deux cas. L'IA est gated sur la clé IA ; la jambe calendrier
respecte son propre gate white-label + toggle runtime.

## Utilisation

- **Activez l'IA** (Settings → AI) et **activez le widget** depuis le dialogue **Customize** de votre propre
  dashboard (« Currency strength » — opt-in, caché par défaut). Le widget montre les devises les plus fortes/faibles
  et l'appel de paire 3M principal ; il lie vers la page complète.
- **Page complète** — `/ai/currency-strength` : un sélecteur d'horizon (1M/3M/6M/12M), un filtre par niveau
  (All/Majors/EM/Exotics), le classement actuel, la prévision forward, la matrice de perspective de paire
  (biais + conviction, signalé pour les adossés/peu confiants), et le récit IA. Appuyez sur **Refresh now**
  (propriétaire) pour régénérer. Un worker background (`App:CurrencyStrength:RefreshEnabled`, **par défaut
  `true`**) rafraîchit selon un calendrier ainsi la page est populated out-of-the-box ; un déploiement ou le
  propriétaire le désactive (ou désactive la fonctionnalité IA / calendrier économique, que le rafraîchisseur
  honore en se dégradant vers aucun snapshot).

## Accès programmatique

Un seul read model partagé (`ICurrencyStrengthQuery`) est accessible de trois façons :

- **IA in-app** — injecté directement (in-process) dans les fonctionnalités IA.
- **MCP** — l'outil `currency_strength` (params `horizon`, `tier`) pour les clients/agents IA.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, sécurisé
  par la **même** machinerie `CalendarJwt` que l'[API REST cBot calendrier](./calendar-cbot-api.md) avec un
  scope **`market:read`** supplémentaire. Un cBot enregistre un client API avec `market:read`, échange son
  id + secret pour un JWT à courte durée de vie à `POST /api/calendar/v1/token`, et appelle les endpoints
  avec un token `Bearer`. Pas de deuxième scheme JWT, pas de deuxième secret — un token fui est read-only,
  scoped marché, à courte durée de vie et révocable.

Voir l'[API REST cBot calendrier](./calendar-cbot-api.md) pour le flux de token et un exemple copy-paste.
