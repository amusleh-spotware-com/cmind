---
description: "Inscription d'utilisateur auto-service sécurisée et gatée white-label — une page d'inscription sur l'application et une API d'approvisionnement serveur-à-serveur, avec des attributs d'utilisateur configurables, gating d'approbation admin ou vérification email et des gardes anti-abus. Désactivé par défaut."
---

# Inscription d'utilisateur

Par défaut, le **propriétaire/administrateur ajoute des utilisateurs manuellement** (page Utilisateurs → *Nouvel utilisateur*). Pour les déploiements white-label qui ont besoin d'intégrer des utilisateurs à grande échelle — ou d'intégrer l'application avec un autre service — cMind livre également un chemin **d'inscription auto-service sécurisée**. Il est **désactivé par défaut** : un déploiement standard est inchangé et la page et l'API retournent 404 jusqu'à ce qu'un déploiement opte pour cela.

Il y a deux points d'entrée partageant un flux de domaine :

1. **Page sur l'application** (`/register`) — une page d'inscription de marque, mobile-first dans la même coque que `/login`.
2. **API d'approvisionnement** (`POST /api/provision`) — un point de terminaison serveur-à-serveur pour qu'un service d'intégration crée des comptes, authentifié par un secret d'approvisionnement par déploiement.

## Qu'est-ce qui est enregistré — minimisation des données

cMind est un **outillage de commerce** : il construit/exécute/backteste les cBots et reflète les transactions sur les identifiants de l'API cTrader Open API *propres* de chaque utilisateur. Il n'**ouvre pas les comptes de trading ou ne garde pas l'argent des clients**, la vérification d'identité KYC/AML est l'obligation du **courtier**, pas celle de cette plateforme. Le formulaire d'inscription enregistre donc **uniquement un email par défaut** — le minimum nécessaire pour fournir le service (RGPD art. 5(1)(c) minimisation de données ; base juridique = contrat). cMind navire délibérément **sans** ID national / date de naissance / champs d'adresse.

Chaque autre attribut est **opt-in par déploiement** via `App:Registration:Attributes`, chacun indépendamment `Off` / `Optional` / `Required` :

| Attribut | Notes |
|---|---|
| `FullName`, `DisplayName`, `Company` | Texte libre, longueur limitée. |
| `Country` | ISO 3166-1 alpha-2, validé contre un ensemble de codes fixe. |
| `Phone` | Format E.164 (`+14155552671`). |
| `Locale` | Forme BCP-47 (`en-US`), normalisée. |
| `MarketingOptIn` | Séparé, case **décochée** — jamais regroupé avec le consentement obligatoire (CAN-SPAM). |
| `AgeConfirmation` | Une case uniquement ; **aucune** date de naissance n'est stockée. |

Les attributs vivent dans l'objet de valeur `UserProfile` détenu par l'agrégat `AppUser`, validés à la construction. **Effacement RGPD** (`AppUser.Anonymize()`) nettoie le profil et tous les jetons de vérification.

**Consentement.** Quand `RequireTermsAcceptance` est activé, l'utilisateur doit accepter les documents juridiques publiés (Conditions, Confidentialité, Divulgation des risques). L'acceptation est enregistrée via l'agrégat `ConsentRecord` existant — version-estampillé, horodaté, avec IP d'origine — le même magasin utilisé ailleurs pour la conservation de dossiers de niveau MiFID/ESMA.

## Modes de gating

Un compte auto-enregistré ne peut pas se connecter jusqu'à ce qu'il efface sa porte (`App:Registration:Mode`) :

- **`AdminApproval`** (par défaut) — le compte est en file d'attente ; un propriétaire/administrateur l'approuve sur la page **Utilisateurs** (section *En attente d'approbation*). N'a pas besoin d'infrastructure mail.
- **`EmailVerification`** — un lien de vérification à usage unique et expirant est envoyé par mail ; le compte s'active quand le lien est ouvert. Nécessite un transport email (`App:Email`). **Si aucun transport n'est configuré, ce mode revient automatiquement à `AdminApproval`** au démarrage, donc l'activation de l'inscription ne casse jamais silencieusement.
- **`Open`** — le compte est actif immédiatement (de confiance/dev uniquement).

Les utilisateurs auto-enregistrés sont toujours créés en tant que **`User`** (ou `Viewer` s'il est configuré) — le domaine **refuse catégoriquement** de frapper Owner/Admin via auto-inscription.

## Sécurité & anti-abus

- **Anti-énumération.** Un email en double donne la **même** réponse neutre `202 Accepted` qu'une nouvelle inscription et ne crée rien — l'application ne divulgue jamais si une adresse a déjà un compte.
- **Limitation de débit.** Les points de terminaison publics sont limités par IP (plus dur que le limiteur d'auth).
- **Politique de mot de passe.** Longueur minimale appliquée ; les mots de passe sont hachés (Argon2 via `IPasswordHasher`) ; les jetons de vérification sont stockés uniquement sous forme de hashes SHA-256 et sont à usage unique + expirant.
- **Hygiène email.** Liste de permission optionnelle de domaines email et blocage de liste noire de fournisseur jetable.
- **CAPTCHA (optionnel).** reCAPTCHA / hCaptcha / Turnstile via leur contrat de vérification partagé.
- **Porte de connexion.** Un compte en attente est refusé à la connexion avec une réponse neutre.

## API d'approvisionnement (intégration)

Avec `App:Registration:Api:Enabled` et un `Secret` défini, un autre service peut créer des utilisateurs :

```
POST /api/provision
X-Provision-Secret: <le secret configuré>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Le secret est comparé en temps constant. Les comptes fournis sont créés **actifs** (ou invités avec `MustChangePassword`) en fonction de `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## L'activer

L'inscription nécessite **à la fois** le drapeau de fonctionnalité et le maître switch :

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // ou EmailVerification / Open
    "DefaultRole": "User",             // jamais Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // vide = toute
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

La section `App:Email` (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`, `FromName`) configure le transport utilisé par le mode `EmailVerification` ; laissez `Host` indéfini pour fonctionner sans mail (l'expéditeur no-op). Voir [basculeurs de fonctionnalités](./feature-toggles.md) et [white-label](./white-label.md) pour la façon dont les déploiements activent les fonctionnalités et changent de marque. Quand l'inscription est activée, la page de connexion affiche un lien **Créer un compte**.

## Testé

Unité (validation de profil, garde de rôle `SelfRegister`, transitions d'activation, jetons à usage unique, effacement), intégration (404 désactivé par défaut, flux d'approbation, rétrogradation de vérification email, anti-énumération, gardes anti-abus, attributs obligatoires, approvisionnement + mauvais secret) et E2E (par défaut la connexion n'a pas de lien d'inscription ; la page `/register` rend son état fermé de marque).
