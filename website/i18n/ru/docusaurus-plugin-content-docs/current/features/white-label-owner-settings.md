---
id: white-label-owner-settings
title: White-label options in Owner settings
sidebar_label: White-label owner settings
---

# White-label options in Owner settings

Каждая white-label опция, которую deployment может установить через конфигурацию (`appsettings`/env),
**также настраивается runtime владельцем приложения**, из **Settings → Deployment**, без redeploy. Owner
override **выигрывает над конфигурацией**; очистка возвращает опцию к deployment's configured (или
built-in default) значению.

Это отражает как white-label *deployment* конфигурирует продукт — те же ручки, тот же эффект —
so an operator может tune branding, gates и policy live и видеть результат немедленно.

## Где это живёт

- **UI:** owner-only **Deployment** section в settings dialog, и deep-linkable page
  **`/settings/deployment`**. Опции сгруппированы в **tab per category** (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, с windowed
  dialog on desktop и full-screen surface on phones.
- **API:** `/api/whitelabel` (owner-only, never feature-gated):
  - `GET /api/whitelabel` — каждая опция с её effective value, provenance (`Config` / `Owner` /
    `Default`) и установлен ли override. **Secrets замаскированы** (value никогда не возвращается).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — установить override (валидируется per option kind). Blank
    value на **secret** сохраняет существующий secret.
  - `DELETE /api/whitelabel/{key}` — очистить один override (вернуться к config).
  - `POST /api/whitelabel/reset` — очистить **все** overrides (вернуться к pure config deployment).

## Как overrides применяются

Owner overrides хранятся как encrypted-where-needed `AppSetting` rows и layered поверх bound
`AppOptions` decorated `IOptionsMonitor<AppOptions>`. Потому что каждый consumer уже читает опции
через that monitor, override применяется **live** across всё приложение — theme, page title, MFA
gate, AI-provider gates, broker allow-list, registration policy, email transport settings и т.д. update
on next read (theme/branding re-render немедленно). Если БД briefly unavailable, слой **fails open**
к настроенному baseline, so override read может никогда не сломать приложение.

**Feature flags** часть той же surface но persisted через existing feature-override
store (`IFeatureGate`), поэтому Features tab и standalone feature toggles никогда не расходятся.

**Secrets** (SMTP password, CAPTCHA secret, provisioning secret) зашифрованы at rest
(`ISecretProtector`, purpose `whitelabel.secret`), write-only в UI, и никогда не возвращаются API.

## Делегированные опции

**Shared Open API application** креденшелы и **per-message-type rate limits** управляются на
**Open API** settings section (см. copy-trading / Open API docs). Они появляются в Deployment
catalog как *delegated* entries (read-only here, со ссылкой) so nothing duplicated и sync
гарантия всё ещё считает их покрытыми.

## Всегда in sync (enforced)

Добавление новой white-label опции в конфигурацию **должно** surface it in owner settings in the same
commit. Это enforced by `WhiteLabelCatalogParityTests`: он reflection'ит over every white-label
options-record property и fails the build unless the property registered in
`Core/WhiteLabel/WhiteLabelCatalog` (or explicitly listed in `IntentionallyExcluded` with a reason).
См. mandate 10 in `CLAUDE.md`.

## Notes

- Enabling SMTP на deployment что стартовал с **нет** email configured needs a restart (sender
  type выбирается at startup); host/credentials already-configured sender update live.
- Option **labels/descriptions** — технические config-knob identifiers shown as data; tab labels и
  all interactive chrome полностью локализованы.
