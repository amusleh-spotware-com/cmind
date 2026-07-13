---
description: "Revendeur remaque l'app — nom du produit, logo, favicon, couleurs, CSS personnalisé — via la config du déploiement, aucun changement de code. Chaque valeur de branding utilise par défaut…"
---

# Branding white-label

Revendeur remaque l'app — nom du produit, logo, favicon, couleurs, CSS personnalisé — via la config du déploiement, aucun changement de code. Chaque valeur de branding **utilise par défaut l'identité standard** : déploiement non configuré ressemble au même qu'avant ; revendeur remplace seulement ce dont il a besoin.

## Modèle

- `Core.Options.BrandingOptions` — lié de `App:Branding`. Basé sur des chaînes (bord de config) ; chaque couleur validée quand le thème est construit.
- `Core.Branding.HexColor` — objet de valeur pour la couleur hex CSS (`#RGB` / `#RRGGBB`), immuable, auto-validant. La couleur invalide lance `DomainException` (`domain.branding.color_invalid`) quand le thème est construit — le déploiement mal configuré échoue rapidement au démarrage, pas rendu de palette brisée.
- `Web.Components.Theme.Build(BrandingOptions)` — produire le thème MudBlazor du branding. Seules les entrées de palette marquées viennent de config ; la typographie, la mise en page, les tons neutres restent fixes afin que le produit garde un look cohérent entre les revendeurs.
- `Web.Branding.IBrandingThemeProvider` — singleton, construire le thème une fois, reconstruire sur changement d'options. Injecté par `MainLayout`/`EmptyLayout` pour `MudThemeProvider`, par la barre d'app pour le nom/logo du produit. `App.razor` lit `IOptionsMonitor<AppOptions>` directement pour la `<head>` de la page (titre, description, favicon, theme-color, CSS personnalisé).

## Configuration

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — copy trading et automatisation de stratégie.",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "AppBarColor": "#0B1220",
      "BackgroundColor": "#0E1525",
      "SurfaceColor": "#161E30",
      "SuccessColor": "#3FB950",
      "ErrorColor": "#F85149",
      "WarningColor": "#D29922",
      "InfoColor": "#2D7FF9",
      "CustomCss": ".mud-appbar { letter-spacing: 1px; }"
    }
  }
}
```

Forme de variable d'environnement : `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Clé | Effet | Défaut |
|-----|--------|---------|
| `ProductName` | Texte de la barre d'app + page `<title>` | `cMind` |
| `LogoUrl` | Image du logo de la barre d'app ; quand vide, texte du nom du produit affiche | *(vide)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | description standard |
| `PrimaryColor` / `SecondaryColor` | accent, icône du tiroir, boutons | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + surfaces ; `AppBarColor` pilote `<meta theme-color>` + manifeste PWA `theme_color`, `BackgroundColor` le `background_color` du manifeste | palette sombre |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | couleurs de statut | standard |
| `CustomCss` | style injecté `<style>` dans `<head>` (déploiement-confiance) | *(vide)* |
| `ShowSiteLink` | afficher le lien de crédit « Powered by cMind » sur le dashboard | `true` |
| `RequireMfa` | exiger que chaque utilisateur configure l'authentification à deux facteurs avant d'utiliser l'app | `false` |
| `NodesUi` | quelle partie de la surface Nodes s'expédie : `Full` (liste + ajouter/supprimer manuellement), `Monitor` (liste en lecture seule, pas d'ajouter/supprimer), `Hidden` (pas de nav, pas de page, pas d'API manuelle) | `Full` |
| `RestrictNodesToOwner` | quand `true`, seul le propriétaire peut voir/gérer les nœuds ; sinon la surface du personnel admin-ou-plus peut. Les utilisateurs normaux ne voient jamais les nœuds de toute façon | `false` |

Les actifs référencés par `LogoUrl`/`FaviconUrl` servis depuis l'app Web `wwwroot` (p.ex. monter le dossier `wwwroot/branding/`) ou n'importe quelle URL absolue.

`App:Branding` validé au démarrage (`BrandingOptionsValidator`, exécuté via `ValidateOnStart`) : chaque couleur doit être hex valide, `CustomCss` ne doit pas contenir `<`/`>` (impossible de s'échapper de la balise `<style>`). Le déploiement mal configuré échoue à démarrer avec un message clair, pas rendu de page cassée.

## Lien Powered-by

Le dashboard rend un petit lien de crédit **« Powered by cMind »** qui pointe vers le site de documentation du projet. Il est contrôlé par `App:Branding:ShowSiteLink` et est **`true` par défaut** — un déploiement non configuré l'affiche. Un revendeur exécutant une instance entièrement white-label définit `App__Branding__ShowSiteLink=false` pour le retirer entièrement.

Le lien est émis par le composant du dashboard et lit le flag par `IBrandingThemeProvider` / `BrandingOptions`, donc le basculer est un changement config-seul (pas de rebuild). Voir [White-label pour le business](../white-label-for-business.md#le-lien-powered-by-cmind) pour le résumé orienté vers le business.

## Liste blanche des courtiers

Un déploiement white-label peut restreindre quels courtiers de comptes de trading ses utilisateurs peuvent ajouter — afin qu'un courtier exécutant cMind pour ses propres clients ne serve jamais que son propre portefeuille. Configuré sous `App:Accounts` :

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Forme de variable d'environnement : `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Comportement :**

- **Liste vide (défaut) ⇒ sans restriction.** Chaque courtier est autorisé et **aucune vérification ne s'exécute** — un déploiement standard est complètement inchangé.
- **Non-vide ⇒ restreint.** cMind vérifie chaque compte qu'un utilisateur essaie d'ajouter contre la liste (insensible à la casse) :
  - **Lien Open API (OAuth)** — le nom du courtier est rapporté avec autorité par l'API cTrader Open, donc un compte non autorisé est simplement **ignoré** (les comptes autorisés dans la même subvention lient quand même) ; la page d'autorisation indique à l'utilisateur quels courtiers ont été ignorés.
  - **cID manuel (nom d'utilisateur / mot de passe)** — le courtier tapé par l'utilisateur n'est **pas** digne de confiance. cMind **vérifie** le vrai courtier du compte en exécutant le cBot broker-probe livré via la cTrader CLI (lisant `Account.BrokerName`) et persiste ce nom vérifié. Un courtier non autorisé est rejeté avec une notification ; un échec de vérification (mauvais identifiants, pas de nœud, timeout) est aussi affiché, et le compte n'est pas ajouté.

**Modèle :**

- `Core.Options.AccountsOptions` — lié de `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`, `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — objet de valeur (trimmé, égalité insensible à la casse).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)` ; vide = autoriser tous. Appliqué comme invariant à l'intérieur de `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount` (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — exécute le conteneur probe sur l'hôte web (qui a la socket Docker), queue les logs, et analyse le courtier via `Core.Accounts.BrokerProbeOutput`. Seulement invoqué quand la liste blanche est restreinte.

**cBot broker-probe :** un pré-construit `broker-probe.algo` s'expédie avec l'app Web (`src/Web/BrokerProbe/`, copié en sortie comme `broker-probe/broker-probe.algo`), donc le défaut `App:Accounts:BrokerProbeAlgoPath` se résout hors de la boîte — un chemin relatif est résolu contre le répertoire de base de l'app, un chemin absolu est utilisé tel quel. Le source vit dans `tools/broker-probe/`. Quand l'algo est absent, la vérification manuelle cID échoue fermé — les comptes sous une liste blanche restreinte peuvent toujours être liés via le chemin Open API, qui n'a besoin d'aucune probe.

## Liste blanche des courtiers — tests

- **Unit** — `UnitTests/Accounts/` : objets de valeur `BrokerName`/`BrokerAllowlist`, parser `BrokerProbeOutput`, et l'invariant liste blanche `CTraderIdAccount`.
- **Integration** — `IntegrationTests/BrokerAllowlistTests.cs` : endpoint cID manuel avec un vérificateur faux (non restreint / vérifié / non autorisé / vérification échouée) + lieur Open API ignorant les comptes non autorisés. `BrokerVerifierLiveTests.cs` exécute la probe **réelle** quand les identifiants cID + l'algo sont fournis (ignore proprement autrement).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs` : un déploiement restreint rejette un ajout manuel via l'UI réelle et affiche la notification « n'a pas pu vérifier » (aucune ligne de compte ajoutée).

## Visibilité UI des nœuds

Les nœuds sont l'infrastructure que la plupart des clients ne gèrent jamais à la main — les agents cTrader CLI [s'enregistrent automatiquement et envoient des heartbeats](../operations/node-discovery.md), donc un déploiement white-label peut masquer les contrôles manuels, ou la surface Nodes entièrement, et toujours exécuter un cluster sain via la découverte automatique. Deux clés de branding config-seul gouvernent ceci :

```json
{
  "App": {
    "Branding": {
      "NodesUi": "Monitor",
      "RestrictNodesToOwner": true
    }
  }
}
```

Forme de variable d'environnement : `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — trois modes :**

- **`Full` (défaut)** — le produit standard : la liste de nœuds plus les contrôles manuels **Nouveau Nœud** et **Supprimer**. `POST`/`DELETE /api/nodes` fonctionnent.
- **`Monitor`** — une surface en lecture seule : la liste et les stats en direct restent, mais l'ajout manuel et la suppression sont retirés. Les nœuds n'apparaissent jamais que via la découverte automatique. `POST`/`DELETE /api/nodes` renvoient **404**.
- **`Hidden`** — le lien de nav Nodes et la page sont complètement partis et la route de la page redirige vers le dashboard ; l'API manuelle d'ajout/suppression est désactivée. Le cluster est découverte automatique seule.

**`RestrictNodesToOwner`** sols qui peut voir et gérer les nœuds. Par défaut `false` garde la surface du personnel **admin-ou-plus** standard (`AdminOrAbove`) ; définissez `true` pour le rendre **propriétaire-seul** (`Owner`). De toute façon **les utilisateurs normaux ne voient jamais les nœuds** — ceci choisit uniquement entre propriétaire-seul et la surface du personnel plus large.

La **découverte automatique des nœuds n'est pas affectée par les deux clés** : le endpoint anonyme d'auto-enregistrement + heartbeat `POST /api/nodes/register` fonctionne toujours, donc un déploiement `Hidden`/`Monitor` grandit toujours son cluster automatiquement.

**Modèle :**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — la source unique de vérité composant le mode + restriction de propriétaire : `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav (`NavMenu.razor`), la page (`Pages/Nodes.razor`) et les endpoints (`NodeEndpoints`) la lisent donc l'UI et l'API ne peuvent jamais désaccorder.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — lié de `App:Branding`.

## Visibilité UI des nœuds — tests

- **Unit** — `UnitTests/Nodes/NodesUiAccessTests.cs` : visibilité de la page, gestion manuelle et résolution de politique requise entre chaque mode + branding par défaut.
- **Integration** — `IntegrationTests/NodeUiGatingTests.cs` : sur HTTP réel + Postgres — `Full` autorise un ajout manuel, `Monitor`/`Hidden` 404 ajout et suppression, et `RestrictNodesToOwner` interdit un admin tandis que le propriétaire lit toujours la liste.
- **E2E** — `E2ETests/NodesUiTests.cs` (défaut `Full` : lien de nav + page + bouton Nouveau Nœud rendent) et `E2ETests/NodesHiddenTests.cs` (`Hidden` : lien de nav parti, `/nodes` redirige).

## Jetons de design (variables CSS)

Le branding atteint aussi la feuille de style **propre** de l'app + les composants personnalisés, pas seulement MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` émet la palette marquée comme propriétés CSS personnalisées sur `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), injectées dans `App.razor` juste après `site.css`. `site.css` et chaque composant lisent `var(--app-*)` — **pas de couleurs dur-codées** — donc une palette de revendeur s'écoule partout (héros de connexion, nav inférieur, conseils d'aide, page hors ligne) gratuitement. Les tons de surface neutre par défaut dans `site.css :root` ; `CustomCss` (injecté en dernier) peut remplacer n'importe quel jeton. Voir [ui-guidelines.md](../ui-guidelines.md) §2.

## PWA marquée

L'app installable est aussi marquée — le endpoint du manifeste (`/manifest.webmanifest`) est construit à partir de `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → thème/background). Voir [pwa.md](pwa.md).

## Tests

- **Unit** — `UnitTests/Branding/HexColorTests.cs` : validation hex valide/invalide.
- **Integration** — `IntegrationTests/ThemeBuildTests.cs` : les couleurs mappent dans la palette, la couleur invalide lance ; `IntegrationTests/BrandingHttpTests.cs` : `ProductName`/description/theme-color personnalisé rendent dans la `<head>` de la page servie (WebApplicationFactory + Postgres), les défauts gardent le nom standard.
- **E2E** — `E2ETests/BrandingTests.cs` : le nom du produit marqué rend dans la barre d'app dans le navigateur réel.
