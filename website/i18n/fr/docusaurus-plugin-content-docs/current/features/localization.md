---
title: Localisation (i18n)
description: cMind est entièrement localisable et est livré dans les mêmes 23 langues que cTrader prend en charge — un trader utilise la plateforme dans sa propre langue.
---

# Localisation (i18n)

cMind est entièrement localisable et est livré dans les **mêmes 23 langues que cTrader lui-même prend en charge**, donc un trader utilise la plateforme — et lit ces docs — dans sa propre langue. L'anglais est le secours ; toute traduction manquante dégradée gracieusement vers l'anglais plutôt que d'afficher un blanc ou une clé brute.

## Langues prises en charge

Arabe (RTL), chinois (simplifié), tchčque, anglais, français, allemand, grec, hongrois, indonésien, italien, japonais, coréen, malais, polonais, portugais (Brésil), russe, serbe, slovaque, slovčne, espagnol, thaï, turc, vietnamien.

La source unique de vérité est `Core.Constants.SupportedCultures` — le middleware de culture de requête, le changeur de langue, le test de parité des ressources, et la porte sans texte codé en dur lisent tous partir de là. Ajouter une langue est un changement d'une ligne là-bas plus ses fichiers de ressources.

## Comment ça fonctionne (Blazor Server)

- **Ressources.** Les chaînes UI vivent dans `src/Web/Resources/Ui.resx` (anglais de base) plus un `Ui.<culture>.resx` par langue. Les composants les lisent via `IStringLocalizer<Ui>` — `@L["clé"]`, jamais un littéral. Les fichiers `.resx` sont générés depuis `tools/i18n/ui-translations.json` (`pwsh tools/i18n/gen-resx.ps1`), la source de vérité conviviale pour les traducteurs.
- **Résolution de culture.** `RequestLocalizationMiddleware` choisit la culture depuis le cookie `.AspNetCore.Culture` d'abord, puis `Accept-Language` du navigateur, puis anglais.
- **Basculement.** Le changeur de langue de la barre d'app (et **Paramètres → Langue**) navigue vers le point de terminaison `GET /set-culture` — un rechargement complet en dehors du circuit Blazor, car un circuit ne peut pas changer de culture en direct. Il écrit le cookie et, pour un utilisateur connecté, persiste le choix dans son profil (`UserProfile.Locale`) ; le rechargement boot une fresh circuit dans la langue choisie.
- **Persistance et login.** Le locale du profil sauvegardé est réécrit dans le cookie de culture à la connexion, donc un utilisateur atterrit dans sa langue sur chaque appareil.
- **Droite-à-gauche.** L'arabe (et toute langue RTL future) définit `<html dir="rtl">` et enveloppe le layout dans `MudRTLProvider` de MudBlazor, reflétant tout le shell.
- **ICU.** L'hôte Web fonctionne avec ICU activé (`InvariantGlobalization=false`) ; le code wire/parse reste sur `CultureInfo.InvariantCulture`, donc seul le formatage UI par culture est affecté — jamais un backtest ou CSV.

## La porte — pas de texte UI codé en dur

Les nouvelles chaînes user-facing **ne peuvent pas** être fusionnées non localisées dans la portée couverte :

- Un test arch-guard qui fail la construction (`NoHardcodedUiTextTests`) scanne les fichiers `.razor` migrés et échoue sur tout littéral, attribut porteur de texte (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`, `aria-label`, `alt`) qui n'est pas une recherche `@L["…"]`.
- Un test de parité des ressources (`ResourceParityTests`) fail la construction si une langue manque une clé ou expédie une valeur vide — chaque langue a toujours chaque clé.

## Ajouter ou changer une chaîne

1. Ajoutez/editez la clé dans `tools/i18n/ui-translations.json` pour **chaque** culture.
2. Regénérez les `.resx` : `pwsh tools/i18n/gen-resx.ps1`.
3. Référencez-la dans le composant avec `@L["votre.clé"]`.
4. `dotnet test` — les portes de parité et de texte codé en dur vous gardent honnête.

## Localisation des docs

Ces docs sont aussi localisées. Docusaurus i18n est configuré pour les 23 locales (`website/i18n/`), avec un dropdown de locale dans la navbar et RTL pour l'arabe. Échafauder les fichiers de traduction d'une locale avec `npm run write-translations -- --locale <code>` et traduire sous `website/i18n/<code>/`. Conformément au mandat de localisation, **ajouter ou changer toute doc signifie mettre à jour chaque locale dans le même changement.**
