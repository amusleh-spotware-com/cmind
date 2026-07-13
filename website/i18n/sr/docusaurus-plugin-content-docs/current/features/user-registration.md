---
description: "Сигурна, white-label-контролисана self-service регистрација корисника — on-app страница за sign-up и server-to-server provisioning API, са конфигурибилним корисничким атрибутима, admin-одобрење или email-verification контролом и заштитом од злоупотребе. Онемогућена по подразумевању."
---

# Регистрација корисника

По подразумевању **власник/админ ручно додаје кориснике** (страница Users → *New User*). За white-label deployment-ове
којима треба да скалирају онбоардинг корисника — или интегришу апликацију са другим сервисом — cMind такође испоручује
**сигурну, self-service регистрациону путању**. Она је **онемогућена по подразумевању**: stock deployment остаје непромењен
и страница и API враћају 404 све док deployment не изабере да укључи.

Постоје две улазне тачке које деле један домен ток:

1. **On-app страница** (`/register`) — брендирана, mobile-first sign-up страница у истој љусци kao и `/login`.
2. **Provisioning API** (`POST /api/provision`) — server-to-server ендпоинт за интегришући сервис да
   креира налоге, аутентикован per-deployment provisioning secret-ом.

## Шта се евидентира — минимизација података

cMind је **алат за трговање**: изграђује/покреће/backtest-ује cBot-ове и огледа трговине преко сваког корисниковог *сопственог*
cTrader Open API креденцијала. Он **не отвара трговачке налоге нити чува новац клијената**, тако да су KYC/AML
верификација идентитета **обaveза брокера**, не ове платформе. Регистрациони формулар стога
евидентира **само email по подразумевању** — минимум потребан за пружање сервиса (GDPR Art. 5(1)(c) data
минимизација; законска основа = уговор). cMind намерно испоручује **без** националног-ID / датума рођења /
пољa за адресу.

Сваки други атрибут је **opt-in per deployment** преко `App:Registration:Attributes`, сваки независно
`Off` / `Optional` / `Required`:

| Атрибут | Напомене |
|---|---|
| `FullName`, `DisplayName`, `Company` | Слободан текст, ограничене дужине. |
| `Country` | ISO 3166-1 alpha-2, валидирано против фиксног скупа кодова. |
| `Phone` | E.164 формат (`+14155552671`). |
| `Locale` | BCP-47 облик (`en-US`), нормализован. |
| `MarketingOptIn` | Одвојена, **непотврђена** кућица — никада не спајати са обавезном сагласношћу (CAN-SPAM). |
| `AgeConfirmation` | Само кућица; **не** чува се датум рођења. |

Атрибути живи у `UserProfile` value object-у који поседује `AppUser` aggregate, валидиран при
конструкцији. **GDPR брисање** (`AppUser.Anonymize()`) чисти профил и било které верификационе токене.

**Сагласност.** Када je `RequireTermsAcceptance` укључен, корисник мора прихватити објављене правне документе
(Terms, Privacy, Risk Disclosure). Прихватање се евидентира кроз постојећи `ConsentRecord` aggregate —
верзионирано, временски означено, ca originating IP — иста складишна која се користи другде за MiFID/ESMA-grade
вођење евиденције.

## Режими контроле

 Самрегистровани налог не може да се пријави док не прође своју контролу (`App:Registration:Mode`):

- **`AdminApproval`** (подразумевано) — налог се ставља у ред; власник/админ га одобрава на страници **Users**
  (*Pending approval* секција). Не треба mail инфраструктура.
- **`EmailVerification`** — једнократни, истекући верификациони линк се шаље email-ом; налог се активира када
  се линк отвори. Потребан je email transport (`App:Email`). **Ако транспорт није конфигурисан, овај режим
  аутоматски downgrades на `AdminApproval`** при покретању, тако да омогућавање регистрације никада не квари тихо.
- **`Open`** — налог је активан одмах (поверено/dev само).

 Самрегистровани корисници се увек креирају kao **`User`** (или `Viewer` ako је конфигурисано) — домен
**одбија** ковање Owner/Admin кроз самрегистрацију.

## Безбедност и заштита од злоупотребе

- **Anti-enumeration.** Дупликат email даје **исти** неутралан `202 Accepted` kao свеж sign-up и
  не креира ништа — апликација никада не открива да ли адреса већ има налог.
- **Rate limiting.** Јавни ендпоинти су throttle-овани per IP (строже од auth limitера).
- **Политика лозинке.** Минимална дужина спроведена; лозинке су хеширане (Argon2 преко `IPasswordHasher`);
  верификациони токени се чувају само kao SHA-256 hash-ови и једнократни су + истекући.
- **Email хигиjена.** Опциони allow-лист email домена и block-листа за disposal провајдере.
- **CAPTCHA (опционо).** reCAPTCHA / hCaptcha / Turnstile преко њиховог дељеног verify уговора.
- **Login gate.** Налог на чекању се одбија при пријави са неутралним одговором.

## Provisioning API (интеграција)

Са `App:Registration:Api:Enabled` и постављеним `Secret`, други сервис може креирати кориснике:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Тајна се упоређује у константном времену. Provisioned налози се креирају **активни** (или позвани са
`MustChangePassword`) зависно од `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Омогућавање

Регистрација захтева **и** feature flag и master прекидач:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // или EmailVerification / Open
    "DefaultRole": "User",             // никада Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // празно = било koji
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

Секција `App:Email` (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`,
`FromName`) конфигурише транспорт koji користи `EmailVerification` режим; остави `Host` неподешен да ради без
mail-а (no-op sender). Види [feature toggles](./feature-toggles.md) и [white-label](./white-label.md) за
начин укључивања функција и rebranding-а. Када je регистрација омогућена, страница за пријаву приказуje link **Креирај
налог**.

## Тестирано

Unit (валидација профила, `SelfRegister` role guard, активационе транзиције, једнократни токени, брисање),
integration (disabled-by-default 404, approval ток, email-verification downgrade, anti-enumeration, заштита од злоупотребе,
обавезни атрибути, provisioning + лоша тајна), и E2E (подразумевано-искljučena пријава нема sign-up линк; `/register`
страница рендерује своје брендирано затворено стање).
