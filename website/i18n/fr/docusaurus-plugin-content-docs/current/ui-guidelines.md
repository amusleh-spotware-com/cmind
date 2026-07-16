---
description: "Obligation pour chaque interface utilisateur nouvelle ou modifiée dans cette application (pages Blazor, dialogues, composants). Ceci est la source de vérité référencée par CLAUDE.md. Si une…"
---

# Directives de conception d'interface utilisateur — OBLIGATOIRE

Obligation pour **chaque** interface utilisateur nouvelle ou modifiée dans cette application (pages Blazor, dialogues, composants).
Ceci est la source de vérité référencée par `CLAUDE.md`. Si une règle vous bloque, arrêtez-vous et posez la question — ne
livrez pas d'interface utilisateur qui la viole. Enraciné dans `plans/ui-overhaul.md`.

## 1. Mobile-first, toujours

- **Créez d'abord pour un téléphone 360–430px**, puis améliorez vers le haut avec des requêtes média `min-width` / propriétés de point d'arrêt MudBlazor. Ne commencez jamais par le bureau avec des remplacements `max-width`.
- **Pas de défilement horizontal à aucune largeur entre 320–1920px.** Si le contenu est plus large que la fenêtre d'affichage, c'est un bug.
- Les cibles tactiles ≥ **44px** (`var(--app-touch-target)`). Les entrées de texte ≥ 16px police (arrête le zoom iOS au focus).
- Respectez les encoches : utilisez `env(safe-area-inset-*)`; la fenêtre d'affichage définit déjà `viewport-fit=cover`.
- Honorez `prefers-reduced-motion` — aucune information essentielle non communiquée que par l'animation.

## 2. Jetons de conception — pas de valeurs en dur

- Toutes les couleurs/rayons/espacements proviennent de **jetons de conception** : thème MudBlazor (`Web/Components/Theme.cs`) +
  les propriétés CSS personnalisées émises par `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Ne codez jamais en dur une couleur hex, un rayon ou une chaîne de marque dans un composant ou une règle CSS.** Lisez un jeton.
  Les jetons proviennent des `BrandingOptions` de marque blanche, donc la palette d'un revendeur doit atteindre votre interface utilisateur gratuitement.
- Nouvelle valeur affectant la marque → ajoutez un jeton + champ de marque ; ne l'intégrez pas en ligne.

## 3. Disposition réactive et données

- **Les tableaux s'effondrent en cartes sur les téléphones.** Chaque `MudTable` définit `Breakpoint="Breakpoint.Sm"` et chaque
  `MudTd` a un `DataLabel`. Pas de tableau brut et large sur mobile. (Modèle : `Components/Pages/Nodes.razor`.)
- Grilles : `MudItem xs="12" sm="6" md="4"` — pleine largeur sur téléphone, multi-colonnes vers le haut.
- Formulaires une colonne sur mobile ; grandes cibles tactiles ; `inputmode`/`autocomplete` sur les entrées ; inputmode numérique/décimal
  pour l'argent/pourcentage.
- **Les contrôles appropriés pour la saisie structurée — jamais une boîte de texte brute pour les nombres ou les listes.** Collectez les nombres,
  l'argent, les pourcentages, les dates, les énumérations et toute donnée multi-valeur avec le contrôle approprié (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, une liste de lignes modifiable de champs typés, ou un tableau), chaque champ
  validé individuellement. Un seul `MudTextField` de texte libre que l'utilisateur doit taper une boîte séparée par des virgules/espaces/sauts de ligne dans — que vous ensuite analysez — est **interdit** : c'est source d'erreurs, non validé et hostile
  sur un téléphone. **Personne ne veut taper une boîte.** La saisie multi-valeur est une liste modifiable de lignes typées (ajouter /
  supprimer), ou est chargée à partir de données de domaine existantes (par exemple, exécutez la vérification directement à partir d'un backtest complété
  plutôt que de re-saisir ses numéros). Le `MudTextField` brut est uniquement pour le texte libre genuine — noms, notes,
  recherche, descriptions.
- Fournissez des états **chargement, vide et erreur** sur chaque liste/détail — dimensionnés pour mobile.
- La **navigation inférieure** mobile (`Components/Layout/BottomNav.razor`) est la navigation téléphone principale ; le
  tiroir groupé est le menu complet. Ajoutez les destinations à fort trafic ; gardez-le ≤5 éléments.

## 4. Dialogues (créer/modifier)

- Toutes les actions ajouter/créer/modifier/nouveau utilisent un **dialogue MudBlazor** (`IDialogService.ShowAsync<TDialog>`), jamais
  un formulaire de page en ligne. Les dialogues se trouvent dans `Web/Components/Dialogs/`, exposent `[Parameter]`s, retournent un
  `public sealed record …Result(...)` imbriqué.
- Les actions de ligne de liste (démarrer/arrêter/supprimer) restent en ligne comme des boutons icône.
- Sur téléphone, les dialogues doivent être **plein écran / pleine largeur** et conscients du clavier.

## 5. Aide en ligne — chaque contrôle

- Chaque option non évidente, sélection, commutateur ou action obtient un **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — survolez sur bureau, **appuyez sur mobile**. Recherchez le texte dans `docs/` pour que
  la guidance reste synchronisée avec le comportement ; mettez à jour les deux dans le même commit.

## 6. Marque blanche

- Le nom du produit, le logo, la description, le support/entreprise, les couleurs, le favicon proviennent tous de `BrandingOptions`.
  Référencez-les (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), jamais le littéral "cMind" ou une
  couleur de marque. Le manifeste PWA, les icônes, la couleur de thème et le héros de connexion sont tous marqués.

## 7. PWA

- L'application est installable. Gardez le point de terminaison du manifeste (`/manifest.webmanifest`) marqué, les icônes présentes
  (192/512/maskable + apple-touch), le worker de service pour les applications shell uniquement (ne touchant jamais le circuit Blazor/`_framework`/hubs), et la
  page hors ligne fonctionnant. Nouvelle route statique → gardez le `scope` du manifeste.
- Blazor Server a besoin d'un circuit SignalR actif → **installable + app-shell**, pas complètement hors ligne. Ne
  promettez pas l'interactivité hors ligne.

## 8. Accessibilité

- Étiquettes sur les entrées, `aria-*` sur les contrôles personnalisés, focus visible, ordre de focus logique. Parce que le thème est
  personnalisable, vérifiez le **contraste** par rapport au thème actif, pas une palette fixe.

## 9. E2E — aucune interface utilisateur ne sort sans tests (bloquant)

Chaque changement orienté utilisateur est livré avec Playwright E2E dans `tests/E2ETests`, conduit comme un vrai utilisateur, **sur émulation de
dispositif mobile** plus bureau :

- Nouvelle route → ajoutez-la à `PageSmokeTests` **et** `MobileLayoutTests` (rendu, nav inférieure, pas d'interface utilisateur d'erreur).
- Convertir un tableau/page → ajouter sa route à l'ensemble mobile **sans débordement**.
- Nouveau flux → un parcours mobile réaliste (créer/modifier/sauvegarder allers-retours) **et** un chemin malheureux
  (saisie invalide, liste vide, permission refusée par rôle).
- Nouveau conseil d'aide → affirmer qu'il s'ouvre au toucher (`HelpTipTests` modèle).
- Utilisez `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (émulation de dispositif).
- `dotnet test` vert avant "fait". WebKit émulé ≠ Safari mobile — la détermination sur appareil réel est une
  étape de version séparée.

## 10. Définition de fait (Interface utilisateur)

- [ ] Mobile-first ; pas de débordement horizontal 320–1920px ; cibles tactiles ≥44px.
- [ ] Uniquement des jetons de conception — zéro couleurs/rayons/chaînes de marque codées en dur.
- [ ] Tableaux → cartes sur téléphone (`DataLabel` + `Breakpoint.Sm`) ; états de chargement/vide/erreur présents.
- [ ] La saisie structurée utilise des contrôles validés appropriés (numérique/date/sélection/liste de lignes modifiable) — pas de boîte
      texte brute que l'utilisateur tape une boîte de valeur/nombre délimitée dans.
- [ ] Créer/modifier via dialogue ; plein écran sur mobile.
- [ ] Chaque contrôle a un `HelpTip` tiré de la documentation.
- [ ] Marque blanche + PWA respectées.
- [ ] E2E mobile + bureau ajouté (smoke, sans-débordement, parcours, chemin malheureux) ; `dotnet test` vert.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` propre sur fichiers touchés.
