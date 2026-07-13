---
slug: /white-label-for-business
title: White-label pour le business
description: Livrez cMind comme votre propre produit marqué — pour les prop firms, les courtiers, et les entreprises de copy-trading. Remaclez chaque surface via config, aucun changement de code.
sidebar_position: 4
---

# cMind White-label pour votre business 🏢

Exécutez une prop firm, un bureau de courtier, ou un service de copy-trading ? cMind a été construit depuis le premier jour pour être **revendu comme votre propre produit**. Chaque surface — le nom, le logo, le favicon, les couleurs, même l'app téléphone installable — se plie à votre marque. Vos clients voient *votre* compagnie. Aucun changement de code, aucun fork, juste config.

:::tip TL;DR
Pointez `App:Branding` vers votre nom, couleurs, et logo. Redémarrez. Fait. La référence technique complète vit dans la [doc de fonctionnalité White-label](./features/white-label.md).
:::

## Ce que vous pouvez remacler

| Surface | Ce qui change |
|---|---|
| **Nom du produit** | Texte de la barre d'app + titre de l'onglet du navigateur |
| **Logo & favicon** | Vos marques partout, incluant l'onglet du navigateur |
| **Couleurs** | Palette complète — primaire, surfaces, couleurs de statut — s'écoule dans l'UI entière *et* le CSS propre de l'app via les jetons de design |
| **App installable (PWA)** | Le nom ajouter-à-l'écran d'accueil, icône, et splash utilisent votre marque |
| **Meta / SEO** | Description et URL d'assistance sont les vôtres |
| **CSS personnalisé** | Injectez votre propre brillant pour les derniers 5% |

Tout utilise par défaut l'identité cMind standard, donc vous remplacez uniquement ce qui vous importe.

## Le rebrand en 60 secondes

Définissez-les sur votre déploiement (config JSON ou variables d'environnement) :

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

Forme de variable d'environnement : `App__Branding__ProductName=AcmeFX`. Les couleurs sont validées au démarrage — une mauvaise valeur hex échoue le boot avec un message clair au lieu de rendre une page cassée. Agréable et bruyant, exactement quand vous le voulez.

## Le lien « Powered by cMind »

Par **défaut**, le dashboard affiche un petit lien **« Powered by cMind »** de bon goût qui ramène les visiteurs sur ce site. C'est activé par défaut parce que nous sommes fiers du projet et cela aide d'autres traders à le trouver — mais c'est **votre choix**.

- **Gardez-le** (défaut) : un lien de crédit subtil sur le dashboard. Vous ne coûte rien, aide le projet.
- **Masquez-le** : définissez `App__Branding__ShowSiteLink=false` et il disparaît entièrement — parfait pour un déploiement entièrement white-label où le produit est indéniablement *le vôtre*.

Voir la [doc de fonctionnalité White-label](./features/white-label.md#powered-by-link) pour savoir exactement où il rend.

## Branding multi-client, par client

Parce que le branding est juste une config de déploiement, chaque déploiement client peut porter sa propre identité. Exécutez une instance séparée par client, ou pilotez le branding depuis votre propre plan de contrôle — l'app le lit depuis `IOptionsMonitor`, donc il peut même reconstruire le thème en direct quand les options changent.

Associez-le avec :

- **[Basculements de fonctionnalité](./features/feature-toggles.md)** — décidez quelles capacités chaque client voit.
- **[Règles de prop-firm](./features/prop-firm.md)** — appliquez vos règles de défi avec suivi d'équité en direct.
- **[Frais de performance](./features/copy-performance-fees.md)** + **[place de marché des fournisseurs](./features/copy-provider-marketplace.md)** — monétisez le copy trading.
- **[Compliance](./features/compliance.md)** — gardez la piste d'audit que votre régulateur demandera.

## Actifs & hébergement

Déposez votre logo/favicon dans le `wwwroot/branding/` de l'app Web (ou pointez `LogoUrl`/`FaviconUrl` vers n'importe quelle URL absolue). Déployez comme bon vous semble — [Docker](./deployment/local.md), [Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md), ou [AWS](./deployment/cloud-aws.md).

Prêt à le rendre vôtre ? Commencez par la [référence technique white-label →](./features/white-label.md)
