---
description: "Защищенная, white-label-гейтированная self-service регистрация пользователей — on-app страница sign-up и server-to-server provisioning API, с конфигурируемыми атрибутами пользователя, admin-approval или email-verification гейтингом и anti-abuse охранниками. Отключено по умолчанию."
---

# Регистрация пользователя

По умолчанию **владелец/admin добавляет пользователей вручную** (Users страница → *New User*). Для white-label развертывания, которым нужно onboard пользователей в масштабе — или интегрировать приложение с другим сервисом — cMind также поставляет **защищенный, self-service регистрационный** путь. Это **отключено по умолчанию**: stock развертывание без изменений и страница и API оба возвращают 404 пока развертывание не opt in.

Есть два entry points разделение одного потока домена:

1. **On-app страница** (`/register`) — брендированная, мобильный-first sign-up страница в той же shell как `/login`.
2. **Provisioning API** (`POST /api/provision`) — server-to-server endpoint для интегрирующего сервиса для создания учетных записей, аутентифицированного per-deployment provisioning секретом.

## Что записывается — data minimization

cMind это trading **tooling**: это строит/запускает/бэктестирует cBots и зеркалит trades через собственные каждого пользователя *own* cTrader Open API учетные данные. Это **не открывает торговые учетные записи или custody денег клиента**, поэтому KYC/AML идентификационная верификация это **broker** обязанность, не этой платформы. Регистрационная форма поэтому записывает **только email по умолчанию** — минимум нужен для предоставления сервиса (GDPR Art. 5(1)(c) data minimization; lawful basis = контракт). cMind намеренно отправляет **нет** национального-ID / дата-рождения / адреса полей.

Каждый другой атрибут это **opt-in per deployment** через `App:Registration:Attributes`, каждый независимо `Off` / `Optional` / `Required`:

| Атрибут | Примечания |
|---|---|
| `FullName`, `DisplayName`, `Company` | Свободный текст, length-bounded. |
| `Country` | ISO 3166-1 alpha-2, валидирован против fixed code set. |
| `Phone` | E.164 формат (`+14155552671`). |
| `Locale` | BCP-47 форма (`en-US`), нормализирован. |
| `MarketingOptIn` | Отдельный, **unticked** checkbox — никогда не bundled с обязательным consent (CAN-SPAM). |
| `AgeConfirmation` | Checkbox только; **нет** дата рождения не хранится. |

Атрибуты живут в value object `UserProfile` owned aggregates `AppUser`, валидированы при конструировании. **GDPR erasure** (`AppUser.Anonymize()`) scrubs профиль и любые verification tokens.

**Согласие.** Когда `RequireTermsAcceptance` включен, пользователь должен принять опубликованные правовые документы (Terms, Privacy, Risk Disclosure). Принятие записано через существующий aggregate `ConsentRecord` — version-stamped, timestamped, с originating IP — тот же store использованный elsewhere для MiFID/ESMA-grade record-keeping.

## Гейтирование режимы

Self-registered учетная запись не может войти пока она не очистит свой gate (`App:Registration:Mode`):

- **`AdminApproval`** (по умолчанию) — учетная запись в очередь; owner/admin одобряет это на **Users** странице (*Pending approval* секция). Не требует mail инфраструктуры.
- **`EmailVerification`** — single-use, expiring верификационная ссылка отправляется по email; учетная запись активирует когда ссылка открыта. Требует email транспорт (`App:Email`). **Если нет транспорта сконфигурирован, этот режим автоматически деградирует в `AdminApproval`** при запуске, поэтому включение регистрации никогда молчаливо не нарушает.**
- **`Open`** — учетная запись активна немедленно (trusted/dev только).

Self-registered пользователи всегда созданы как **`User`** (или `Viewer` если сконфигурирован) — домен **hard-refuses** minting Owner/Admin через self-registration.

## Безопасность & anti-abuse

- **Anti-enumeration.** Duplicate email выход **тот же** нейтральный `202 Accepted` как свежий sign-up и создает ничего — приложение никогда не раскрывает является ли адрес уже имеет учетную запись.
- **Rate limiting.** Публичные endpoints throttled per IP (жестче чем auth limiter).
- **Password политика.** Минимальная длина enforced; пароли хешированы (Argon2 через `IPasswordHasher`); verification tokens хранятся только как SHA-256 хеши и single-use + expiring.
- **Email гигиена.** Опциональный allow-list email domains и disposable-provider block-list.
- **CAPTCHA (опциональный).** reCAPTCHA / hCaptcha / Turnstile через их общий verify контракт.
- **Login gate.** Pending учетная запись отказана при входе с нейтральным ответом.

## Provisioning API (интеграция)

С `App:Registration:Api:Enabled` и `Secret` установленным, другой сервис может создать пользователей:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Secret сравнивается в constant time. Provisioned учетные записи созданы **active** (или приглашены с `MustChangePassword`) в зависимости от `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Включение этого

Регистрация требует **оба** feature flag и master switch:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // или EmailVerification / Open
    "DefaultRole": "User",             // никогда Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // empty = любой
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

`App:Email` секция (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`, `FromName`) конфигурирует транспорт используемый `EmailVerification` режимом; оставьте `Host` unset для запуска с нет mail (no-op sender). Смотрите [feature toggles](./feature-toggles.md) и [white-label](./white-label.md) для как развертывания включают функции и rebrand. Когда регистрация включена, login страница показывает **Create account** ссылку.

## Протестировано

Unit (profile validation, `SelfRegister` role guard, activation transitions, single-use tokens, erasure), интеграция (disabled-by-default 404, approval flow, email-verification downgrade, anti-enumeration, abuse guards, required attributes, provisioning + bad secret) и E2E (default-off login нет sign-up ссылки; `/register` страница рендерит свою брендированную закрытое состояние).
