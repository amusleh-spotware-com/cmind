---
id: white-label-owner-settings
title: Options white-label dans les paramètres Owner
sidebar_label: Paramètres white-label owner
---

# Options white-label dans les paramètres Owner

Chaque option white-label qu'un déploiement peut définir via la configuration (`appsettings`/env) est
**aussi paramétrable à l'exécution par le propriétaire de l'application**, depuis **Settings → Deployment**,
sans redeploy. Un écrasement owner **l'emporte sur la configuration** ; l'effacer ramène l'option à la valeur
configurée par le déploiement (ou par défaut intégrée).

Cela reflète la façon dont un déploiement white-label *configure* le produit — les mêmes knobs, le même effet
— ainsi un opérateur peut tuner le branding, les gates et la politique live et voir le résultat immédiatement.

## Où ça vit

- **UI :** la section **Deployment** owner-only dans le dialogue des paramètres, et la page profonde
  **`/settings/deployment`**. Les options sont regroupées en **un onglet par catégorie** (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, avec un dialogue fenêtré
  sur desktop et une surface full-screen sur téléphones.
- **API :** `/api/whitelabel` (owner-only, jamais gated par fonctionnalité) :
  - `GET /api/whitelabel` — chaque option avec sa valeur effective, provenance (`Config` / `Owner` /
    `Default`) et si un écrasement est défini. **Les secrets sont masqués** (valeur jamais retournée).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — définir un écrasement (validé par type d'option).
    Une valeur vide sur un **secret** conserve le secret existant.
  - `DELETE /api/whitelabel/{key}` — effacer un écrasement (retour à la config).
  - `POST /api/whitelabel/reset` — effacer **tous** les écrasements (ramener le déploiement à la pure config).

## Comment les écrasements prennent effet

Les écrasements owner sont stockés comme lignes `AppSetting` chiffrées si nécessaire et superposés sur le
`AppOptions` bindé par un `IOptionsMonitor<AppOptions>` décoré. Parce que chaque consommateur lit déjà les
options à travers ce monitor, un écrasement s'applique **live** à travers toute l'application — le thème,
le titre de page, le gate MFA, les gates fournisseur IA, la allow-list broker, la politique d'enregistrement,
les paramètres de transport email, etc. se mettent à jour au prochain read (le thème/le branding se
ré-affichent immédiatement). Si la base de données est brièvement indisponible, la couche **fail open** vers
la baseline configurée, ainsi un read d'écrasement ne peut jamais casser l'application.

**Les feature flags** font partie de la même surface mais sont persistés à travers le store de override
de fonctionnalité existant (`IFeatureGate`), ainsi l'onglet Features et les bascules de fonctionnalité
individuelles ne divergent jamais.

**Les secrets** (mot de passe SMTP, secret CAPTCHA, secret de provisioning) sont chiffrés au repos
(`ISecretProtector`, purpose `whitelabel.secret`), write-only dans l'UI, et jamais retournés par l'API.

## Options déléguées

Les **credentials de l'application Open API partagée** et les **limites de débit par type de message** sont
gérés dans la section de paramètres **Open API** (voir les docs copy-trading / Open API). Ils apparaissent
dans le catalogue Deployment comme entrées *déléguées* (read-only ici, avec un lien) ainsi rien n'est
dupliqué et la garantie de sync s'applique quand même.

## Toujours synchronisés (appliqué)

Ajouter une nouvelle option white-label à la configuration **doit** la surfacedans les paramètres owner
dans le même commit. Ceci est appliqué par `WhiteLabelCatalogParityTests` : il fait de la réflexion sur
chaque propriété de l'options-record white-label et fail la build à moins que la propriété soit enregistrée
dans `Core/WhiteLabel/WhiteLabelCatalog` (ou explicitement listée dans `IntentionallyExcluded` avec une
raison). Voir le mandat 10 dans `CLAUDE.md`.

## Notes

- Activer SMTP sur un déploiement qui a commencé avec **aucun** email configuré nécessite un redémarrage
  (le type d'expéditeur est choisi au démarrage) ; host/credentials d'un expéditeur déjà configuré se
  mettent à jour live.
- Les **labels/descriptions d'option** sont des identifiants techniques du knob de config affichés
  comme données ; les labels d'onglet et tout le chrome interactif sont entièrement localisés.
