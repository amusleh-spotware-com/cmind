---
description: "Опциональная TOTP двухфакторная аутентификация с регистрацией приложения authenticator, одноразовыми backup кодами и white-label переключателем для обязательности для всех пользователей."
---

# Двухфакторная аутентификация (2FA)

Учетные записи могут быть защищены **time-based one-time password (TOTP)** двухфакторной аутентификацией поверх пароля. Это **opt-in** из профиля пользователя по умолчанию, и white-label развертывание может сделать это **обязательным** для всех. Любое RFC 6238 authenticator приложение работает — Google Authenticator, Microsoft Authenticator, Authy, Aegis, FreeOTP — потому что реализация стандартная (SHA-1, 6 цифр, 30-секундный step); нет proprietary компонента сервера.

## Как это работает

- **Домен.** MFA живет на aggregates `AppUser` (Access контекст). Пользователь зарегистрирован через intention-revealing методы — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`, `RegenerateBackupCodes`, `DisableMfa` — поэтому инварианты (secret должен быть подтвержден перед активацией; backup код является single-use) enforced в одном месте.
- **TOTP.** Поколение и верификация сидят позади Core интерфейса `ITotpAuthenticator`, реализованного в Infrastructure с библиотекой **Otp.NET**. Верификация толерирует ±1 time-step clock skew.
- **Secret в покое.** Authenticator secret хранится **зашифрованный** через `ISecretProtector` (`EncryptionPurposes.MfaSecret`) — никогда plaintext.
- **Backup коды.** Десять single-use recovery кодов выданы при регистрации, показаны **один раз**, и хранятся только как SHA-256 хеши (`MfaBackupCodes`). Каждый работает ровно один раз; spent код отклоняется впоследствии.

## Включение его (профиль)

На **Account** странице (`/account`) *Two-factor authentication* секция показывает текущий статус:

1. **Enable two-factor** открывает MudBlazor диалог с **QR кодом** (отрендерен server-side как SVG через `Net.Codecrete.QrCodeGenerator`) плюс manual setup key.
2. Отсканируйте его, введите 6-цифровой код для подтверждения — это верифицирует pending secret перед активацией.
3. Диалог затем показывает **backup коды**; сохраните их. 2FA теперь включена.

Та же секция позволяет зарегистрированному пользователю **переразработать backup коды** или **выключить** 2FA — оба требуют пароль учетной записи для подтверждения.

## Вход в систему с 2FA

Вход это **двухэтапный** поток один раз 2FA включена:

1. **Password шаг** (`POST /api/auth/login`). При успехе auth cookie **не** выдана еще; вместо этого короткоживая (5-минутная), зашифрованная *pending* cookie установлена и пользователь отправлен на `/login/2fa`.
2. **Challenge шаг** (`POST /api/auth/login/verify-2fa`). Пользователь вводит TOTP код **или** любой неиспользованный backup код. При успехе pending cookie удалена и реальный auth cookie выдан.

Неудачные попытки second-factor подсчитываются в сторону существующей учетной записи **lockout** (`AuthLockout`), и endpoints auth rate-limited.

## Обязательный 2FA для white-label развертывания

Регулируемый перепродавец может требовать 2FA для **каждой** учетной записи:

```jsonc
// appsettings / environment
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Когда `RequireMfa` включен и пользователь без 2FA входит, password шаг отчеты `mfaSetupRequired` и `MfaEnforcementMiddleware` переходят их страницу навигации в `/account` пока они не закончат регистрацию. Он по умолчанию `false`, поэтому unconfigured развертывание сохраняет 2FA опциональным. Смотрите [White-label](white-label.md).

## Endpoints

| Метод & маршрут | Цель |
| --- | --- |
| `POST /api/auth/login` | Password шаг; возвращает `mfaRequired` (challenge) или входит |
| `POST /api/auth/login/verify-2fa` | Second-factor шаг (TOTP или backup код) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, pending, оставшийся backup-code счет |
| `POST /api/auth/mfa/setup` | Начать регистрацию — возвращает secret, `otpauth://` URI, QR SVG |
| `POST /api/auth/mfa/confirm` | Подтвердить код, активировать, вернуть backup коды |
| `POST /api/auth/mfa/disable` | Выключить (password-confirmed) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Выдать свежий набор (password-confirmed) |

## Тесты

- **Unit** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (RFC 6238 vectors), `AppUserMfaTests.cs` (enrollment/transition/single-use инварианты), `MfaBackupCodesTests.cs`.
- **Интеграция** — `IntegrationTests/MfaPersistenceTests.cs` (enroll → confirm → consume, cascade delete) и `MfaFlowTests.cs` (полный HTTP двухэтапный вход с TOTP + backup код и обязательный-enrollment gate).
- **E2E** — `E2ETests/MfaFlowTests.cs`: включить из профиля (QR + confirm + backup коды) и завершить challenged sign-in, на desktop и mobile viewports.
