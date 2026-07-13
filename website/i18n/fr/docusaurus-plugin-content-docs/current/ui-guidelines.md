---
description: "Contraignant pour chaque pièce d'UI nouvelle ou changée dans cette app (pages Blazor, boîtes de dialogue, composants). C'est la source de vérité référencée par CLAUDE.md. Si un…"
---

# Directives de conception UI — OBLIGATOIRE

Contraignant pour **chaque** pièce d'UI nouvelle ou changée dans cette app (pages Blazor, boîtes de dialogue, composants).
C'est la source de vérité référencée par `CLAUDE.md`. Si une règle vous bloque, arrêtez et posez la question — ne
fournissez pas d'UI qui la viole. Enracinée dans `plans/ui-overhaul.md`.

## 1. Mobile-first, toujours

- **Rédigez pour un téléphone 360–430px d'abord**, puis améliorez vers le haut avec `min-width` requêtes de média / props breakpoint MudBlazor.
  N'allez jamais desktop-first avec remplacements `max-width`.
- **Pas de scroll horizontal à aucune largeur 320–1920px.** Si le contenu est plus large que la fenêtre d'affichage, c'est un bug.
- Les cibles tactiles ≥ **44px** (`var(--app-touch-target)`). Les entrées de texte ≥ 16px police (empêche iOS zoom-on-focus).
- Respectez les encoches : utilisez `env(safe-area-inset-*)` ; la fenêtre d'affichage définit déjà `viewport-fit=cover`.
- Honorez `prefers-reduced-motion` — pas d'info essentielle transmise uniquement par animation.

## 2. Tokens de conception — pas de valeurs en dur

- Tous les couleur/radius/spacing viennent des **tokens de conception** : thème MudBlazor (`Web/Components/Theme.cs`) +
  les propriétés personnalisées CSS émises par `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Ne codez jamais en dur une couleur hex, radius, ou chaîne de marque dans un composant ou règle CSS.** Lisez un token.
  Les tokens s'écoulent de la `BrandingOptions` white-label, de sorte que la palette d'un revendeur doit atteindre votre UI gratuitement.
- Nouvelle valeur affectant la marque → ajoutez un token + champ de branding ; ne l'incorporez pas.

## 3. Disposition réactive & données

- **Les tableaux s'effondrent en cartes sur les téléphones.** Chaque `MudTable` définit `Breakpoint="Breakpoint.Sm"` et chaque
  `MudTd` a un `DataLabel`. Pas de tableau large brut sur mobile. (Modèle : `Components/Pages/Nodes.razor`.)
- Grilles : `MudItem xs="12" sm="6" md="4"` — pleine largeur sur téléphone, multi-colonne vers le haut.
- Formulaires colonne unique sur mobile ; grandes cibles tactiles ; `inputmode`/`autocomplete` sur entrées ; inputmode numérique/décimal
  pour argent/pourcentage.
- Fournissez des **chargement, vide, et erreur** états sur chaque list/detail — dimensionné pour mobile.
- La **navigation inférieure** du mobile (`Components/Layout/BottomNav.razor`) est la nav téléphone primaire ; le
  tiroir groupé est le menu complet. Ajoutez là les destinations à fort trafic ; gardez-le ≤5 articles.

## 4. Boîtes de dialogue (créer/éditer)

- Toutes les actions ajouter/créer/éditer/nouveau utilisent une **boîte de dialogue MudBlazor** (`IDialogService.ShowAsync<TDialog>`), jamais
  un formulaire de page incorporé. Les boîtes de dialogue vivent dans `Web/Components/Dialogs/`, exposent les `[Parameter]`s, retournent un imbriqué
  `public sealed record …Result(...)`. Les actions de rangée de liste (démarrage/arrêt/suppression) restent incorporées comme boutons d'icône.
- Sur les téléphones, les boîtes de dialogue doivent être **plein écran / pleine largeur** et keyboard-aware.

## 5. Aide incorporée — chaque contrôle

- Chaque option non-évidente, select, switch, ou action obtient une **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — survol sur desktop, **appuyez sur mobile**. Sourcez le texte depuis `docs/` pour que
  les conseils restent en sync avec le comportement ; mettez à jour les deux dans le même commit.

## 6. White-label

- Le nom du produit, logo, description, support/société, couleurs, favicon viennent tous de `BrandingOptions`.
  Référencez-les (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), jamais littéral "cMind" ou un couleur de marque. Le manifest PWA, icônes, theme-color, et héros de connexion sont tous marqués.

## 7. PWA

- L'app est installable. Gardez l'endpoint manifest (`/manifest.webmanifest`) marqué, les icônes présentes
  (192/512/maskable + apple-touch), le service worker app-shell-only (ne touchant jamais le circuit Blazor/`_framework`/hubs), et la page hors ligne fonctionnant. Nouvelle route statique → gardez manifest `scope`.
- Blazor Server a besoin d'un circuit SignalR en direct → **installable + app-shell**, pas entièrement hors ligne. Ne
  promettez pas l'interactivité hors ligne.

## 8. Accessibilité

- Étiquettes sur entrées, `aria-*` sur contrôles personnalisés, focus visible, ordre de focus logique. Parce que le thème est
  white-labelable, vérifiez le **contraste** contre le thème actif, pas une palette fixe.

## 9. E2E — pas d'UI n'est fourni sans test (bloquant)

Chaque changement du côté utilisateur livre Playwright E2E dans `tests/E2ETests`, conduit comme un vrai utilisateur, **sur l'émulation du
appareil mobile** plus desktop :

- Nouvelle route → l'ajouter à `PageSmokeTests` **et** `MobileLayoutTests` (s'affiche, nav inférieure, pas UI d'erreur).
- Convertir un tableau/page → ajouter sa route à l'ensemble mobile **sans-overflow**.
- Nouveau flux → un voyage mobile réaliste (créer/éditer/enregistrer aller-retour) **et** un chemin malheureux
  (entrée invalide, liste vide, permission-déniée par rôle).
- Nouveau help tip → affirmer qu'il s'ouvre sur appui (`HelpTipTests` modèle).
- Utilisez `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (émulation d'appareil).
- `dotnet test` vert avant « fait ». WebKit émulé ≠ Safari mobile — la gation d'appareil réel est une étape de sortie séparée.

## 10. Définition de fait (UI)

- [ ] Mobile-first ; pas de débordement horizontal 320–1920px ; cibles tactiles ≥44px.
- [ ] Uniquement tokens de conception — zéro couleurs/radii/chaînes de marque en dur.
- [ ] Tableaux → cartes sur téléphone (`DataLabel` + `Breakpoint.Sm`) ; états chargement/vide/erreur présents.
- [ ] Créer/éditer via boîte de dialogue ; plein écran sur mobile.
- [ ] Chaque contrôle a un `HelpTip` sourçé depuis docs.
- [ ] White-label + PWA respectés.
- [ ] E2E mobile + desktop ajouté (smoke, sans-overflow, voyage, chemin malheureux) ; `dotnet test` vert.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` propre sur fichiers touchés.
