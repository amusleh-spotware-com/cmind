# Force de devise macro IA et perspective directionnelle

cMind livre un moteur de force de devise macro **assisté par IA, déterministe mathématiquement**. Il classe un univers configurable de devises — les 8 majeurs plus les devises des marchés émergents et exotiques — par force fondamentale **actuelle** et projette une **perspective directionnelle prospective** pour chaque paire sur un horizon choisi (1M / 3M / 6M / 12M). Chaque classement, chaque biais de paire et chaque nombre est calculé par des mathématiques pures déterministes dans le cœur du domaine ; le LLM rassemble uniquement les entrées prospectives que les données ne peuvent pas publier et *explique* le résultat en anglais clair. Il n'invente jamais un classement, une direction ou un nombre.

> **Limitation honnête.** Les fondamentaux prédisent bien la valeur à moyen-long terme et mal la valeur à court terme. Traitez ceci comme un filtre de positionnement / confluence, **pas** un signal de synchronisation à court terme. Les lectures proches des sorties à fort impact (NFP/CPI/banque centrale) sont bruyantes. Pas de conseil financier.

## Comment ça marche

1. **Les fondamentaux actuels proviennent du calendrier économique, pas du LLM.** Les chiffres durs — taux de politique, CPI vs cible, PIB, emploi, balance commerciale — et leurs **z-scores de surprise** sont sourcés **point-dans-le-temps** à partir du module de [calendrier économique](./economic-calendar.md) (FRED/BLS/BEA/ECB et calendriers des banques centrales). Un snapshot historique ne fuit jamais l'anticipation.
2. **Le LLM rassemble uniquement ce que le calendrier ne peut pas publier** — par devise : la **trajectoire prospective** (chemin attendu du taux de politique en bp, inflation-tendance-vs-cible, momentum de croissance) et une **perspective géopolitique** (risk-on/off, tarifs, fiscal/dette, élections), plus toute figure actuelle EM/exotique que le calendrier manque. JSON strict, validation consciente du tier, recherche Web activée.
3. **Le domaine calcule le classement et la matrice prospective déterministiquement.** Chaque moteur est noté en tant que **z-score intra-tier** (donc une inflation de 50 % exotique ne distord jamais les majeurs), winsorisé, somme pondérée dans un composite, et classé le plus fort→le plus faible avec un bris de lien ISO stable. La couche prospective porte chaque composite le long de sa trajectoire — `projected = current + horizonScale · Σ trajectoryDriver·weight` — et mappe le différentiel de chaque paire aux **biais directionnels** (▲ apprécier / ▬ neutre / ▼ déprécier) avec une conviction.
4. **Le LLM explique** le classement et les appels de meilleure paire en langage clair.

## Les moteurs

| Moteur | Effet sur la force | Notes |
|---|---|---|
| Taux de politique & trajectoire | Plus élevé / hawkish ⇒ plus fort | Poids le plus élevé ; la divergence des banques centrales entraîne les plus grands écarts. |
| Inflation (CPI vs cible) | Au-dessus de la cible ⇒ plus faible | Noté inversement (traînée du pouvoir d'achat). |
| Croissance du PIB | Croissance relative plus élevée ⇒ plus forte | Différentiel vs le panel. |
| Emploi | Main-d'œuvre plus forte ⇒ plus forte | Alimente le chemin de la politique. |
| Balance commerciale / compte courant | Surplus ⇒ plus fort | Demande structurelle. |
| Stance de politique | Hawkish ⇒ plus fort | Le moteur long terme primaire. |
| Momentum de surprise | Récents succès ⇒ plus fort | Des z-scores de surprise du calendrier. |
| Géopolitique / Risque | Risk-off ⇒ refuges sûrs (USD/JPY/CHF) plus fort | Delta de risque prospective borné. |
| Rendement réel / carry *(EM/exotique)* | Taux réel positif ⇒ plus fort | Moteur EM dominant dans les régimes calmes. |
| Vulnérabilité externe *(EM/exotique)* | Déficits / faibles réserves / dette USD ⇒ plus faible | Pression de dépréciation structurelle. |
| Termes de l'échange *(exportateurs de matières premières)* | Hausse des prix à l'exportation ⇒ plus fort | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Risque politique / institutionnel *(EM/exotique)* | Instabilité ⇒ plus faible | Bande morte plus large, conviction plafonnée. |

## Univers étage (majeurs + EM + exotiques)

L'univers est **configurable en déploiement** (`App:CurrencyStrength:Universe`) — ajouter une devise est une config, pas du code. Chaque devise porte un **tier** (`Major` / `EmergingMarket` / `Exotic`) qui règle la pondération, la largeur de bande morte et le plafond de conviction :

- **Majeurs** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (menés par le niveau de taux).
- **Marchés émergents** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK) ; carry + risque + vulnérabilité-externe pondérée, confiance moyenne.
- **Exotiques** — TRY, HUF, CZK, plus HKD/SAR indexés sur USD ; confiance faible, bande morte plus large, conviction plafonnée. Les devises **indexées / fortement gérées** (HKD, SAR, CNH) sont signalées, leur trajectoire est sous-pondérée et leur perspective de paire est serrée vers `Neutre` afin qu'un indexage ne soit jamais lu comme un signal flottant libre.

Parce que les statistiques officielles EM/exotiques sont de plus basse fréquence, révisées et parfois opaques, les chiffres rassemblés par l'IA portent une **confiance par tier** affichée en tant que badge de fiabilité.

## Dégradation gracieuse

| Calendrier | IA | Résultat |
|---|---|---|
| ✅ | ✅ | Classement complet + projection prospective + récit (`CalendarAndAi`). |
| ✅ | ❌ | Classement actuel calendrier-seulement, pas de projection prospective (`CalendarOnly`). |
| ❌ | ✅ | Chiffres actuels rassemblés par IA + prospective, confiance inférieure (`AiOnly`). |
| ❌ | ❌ | Pas de snapshot — le widget se cache et la page affiche un état vide. |

L'application tourne inchangée de toute façon. L'IA est gatée sur la clé IA ; la jambe du calendrier respecte sa propre porte white-label + bascule runtime.

## L'utiliser

- **Activer l'IA** (Paramètres → IA) et **activer le widget** à partir de votre propre tableau de bord **Personnaliser** la boîte de dialogue ("Force de devise" — opt-in, masqué par défaut). Le widget affiche les principales devises fortes/faibles et l'appel de paire 3M principal ; il lie vers la page complète.
- **Page complète** — `/ai/currency-strength` : sélecteur d'horizon (1M/3M/6M/12M), filtre de tier (Tous/Majeurs/EM/Exotiques), le classement actuel, la prévision prospective, la matrice des perspectives de paire (biais + conviction, indexés/confiance-faible signalés), et le récit IA. Appuyez sur **Rafraîchir maintenant** (propriétaire) pour régénérer. Un worker d'arrière-plan (`App:CurrencyStrength:RefreshEnabled`, **par défaut `true`**) se rafraîchit selon un horaire afin que la page soit peuplée prête à l'emploi ; un déploiement ou le propriétaire la désactive (ou désactive la fonctionnalité IA / calendrier économique, que le rafraîchisseur honore en se dégradant sans snapshot).

## Accès programmatique

Un modèle de lecture partagé (`ICurrencyStrengthQuery`) est accessible trois façons :

- **IA in-app** — injecté directement (in-process) dans les fonctionnalités IA.
- **MCP** — l'outil `currency_strength` (params `horizon`, `tier`) pour les clients IA/agents.
- **REST cBot** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, sécurisé par la **même** machinerie `CalendarJwt` que l'[API calendrier cBot](./calendar-cbot-api.md) avec une portée **`market:read`** ajoutée. Un cBot enregistre un client API avec `market:read`, échange son id + secret pour un JWT de courte durée sur `POST /api/calendar/v1/token`, et appelle les points de terminaison avec un jeton `Bearer`. Pas de deuxième schéma JWT, pas de deuxième secret — un jeton divulgué est read-only, scoped de marché, courte durée et révocable.

Voir l'[API calendrier cBot](./calendar-cbot-api.md) pour le flux de jetons et un exemple copy-paste.
