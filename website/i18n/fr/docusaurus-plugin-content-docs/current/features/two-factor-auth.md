---
description: "Authentification à deux facteurs TOTP optionnelle avec inscription d'application d'authentificateur, codes de secours à usage unique et un switch white-label pour le rendre obligatoire pour tous les utilisateurs."
---

# Authentification à deux facteurs (2FA)

Les comptes peuvent être protégés avec **mot de passe unique basé sur le temps (TOTP)** authentification à deux facteurs en plus du mot de passe. Elle est **opt-in** depuis le profil de l'utilisateur par défaut, et un déploiement white-label peut la rendre **obligatoire** pour tout le monde. N'importe quelle application d'authentificateur RFC 6238 fonctionne — Google Authenticator, Microsoft Authenticator, Authy, Aegis, FreeOTP — car l'implémentation est standard (SHA-1, 6 chiffres, étape de 30 secondes) ; aucun composant serveur propriétaire n'est impliqué.

## Comment ça marche

- **Domaine.** MFA vit sur l'agrégat `AppUser` (contexte Access). Un utilisateur s'inscrit via des méthodes révélant l'intention — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`, `RegenerateBackupCodes`, `DisableMfa` — afin que les invariants (un secret doit être confirmé avant d'être activé ; un code de secours est d'usage unique) soient appliqués en un seul endroit.
- **TOTP.** La génération et la vérification s'assoient derrière l'interface Core `ITotpAuthenticator`, implémentée en Infrastructure avec la bibliothèque **Otp.NET**. La vérification tolère ±1 décalage d'étape de temps.
- **Secret au repos.** Le secret d'authentificateur est stocké **chiffré** via `ISecretProtector` (`EncryptionPurposes.MfaSecret`) — jamais en clair.
- **Codes de secours.** Dix codes de récupération à usage unique sont émis à l'inscription, affichés **une fois**, et stockés uniquement sous forme de hashes SHA-256 (`MfaBackupCodes`). Chacun fonctionne exactement une fois ; un code dépensé est rejeté par la suite.

## L'activer (profil)

Sur la page **Compte** (`/account`) la section *Authentification à deux facteurs* affiche l'état actuel :

1. **Activer deux facteurs** ouvre une boîte de dialogue MudBlazor avec un **code QR** (rendu côté serveur sous forme de SVG via `Net.Codecrete.QrCodeGenerator`) plus la clé de configuration manuelle.
2. Scannez-le, entrez le code de 6 chiffres pour confirmer — cela vérifie le secret en attente avant l'activation.
3. La boîte de dialogue affiche alors les **codes de secours** ; sauvegardez-les. 2FA est maintenant activé.

La même section permet à un utilisateur inscrit de **regénérer les codes de secours** ou **désactiver** 2FA — tous deux nécessitent le mot de passe du compte pour confirmer.

## Connexion avec 2FA

La connexion est un flux **en deux étapes** une fois que 2FA est activé :

1. **Étape de mot de passe** (`POST /api/auth/login`). En cas de succès, le cookie d'auth n'est **pas** encore émis ; à la place un cookie *en attente* chiffré de courte durée (5 minutes) est défini et l'utilisateur est envoyé à `/login/2fa`.
2. **Étape de défi** (`POST /api/auth/login/verify-2fa`). L'utilisateur entre un code TOTP **ou** n'importe quel code de secours inutilisé. En cas de succès, le cookie en attente est déposé et le vrai cookie d'auth est émis.

Les tentatives de facteur second échoué comptent vers l'**verrouillage** du compte existant (`AuthLockout`), et les points de terminaison d'auth sont limités en débit.

## 2FA obligatoire pour un déploiement white-label

Un revendeur régulé peut exiger 2FA pour **chaque** compte :

```jsonc
// appsettings / environnement
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Quand `RequireMfa` est activé et qu'un utilisateur sans 2FA se connecte, l'étape de mot de passe rapporte `mfaSetupRequired` et `MfaEnforcementMiddleware` redirige sa navigation de page vers `/account` jusqu'à terminer l'inscription. Il revient par défaut à `false`, afin qu'un déploiement non configuré garde 2FA optionnel. Voir [White-label](white-label.md).

## Points de terminaison

| Méthode & route | Objectif |
| --- | --- |
| `POST /api/auth/login` | Étape de mot de passe ; retourne `mfaRequired` (défi) ou se connecte |
| `POST /api/auth/login/verify-2fa` | Étape de facteur second (TOTP ou code de secours) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, en attente, nombre de codes de secours restants |
| `POST /api/auth/mfa/setup` | Débuter l'inscription — retourne secret, URI `otpauth://`, SVG QR |
| `POST /api/auth/mfa/confirm` | Confirmer un code, activer, retourner codes de secours |
| `POST /api/auth/mfa/disable` | Désactiver (confirmé par mot de passe) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Émettre un ensemble frais (confirmé par mot de passe) |

## Tests

- **Unité** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (vecteurs RFC 6238), `AppUserMfaTests.cs` (invariants d'inscription/transition/usage unique), `MfaBackupCodesTests.cs`.
- **Intégration** — `IntegrationTests/MfaPersistenceTests.cs` (inscrire → confirmer → consommer, suppression en cascade) et `MfaFlowTests.cs` (flux de connexion HTTP en deux étapes complet avec TOTP + code de secours, et la porte d'inscription obligatoire).
- **E2E** — `E2ETests/MfaFlowTests.cs` : activer depuis le profil (QR + confirmer + codes de secours) et compléter une connexion contestée, sur les fenêtres d'affichage de bureau et mobile.
