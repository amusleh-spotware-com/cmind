---
id: white-label-owner-settings
title: White-label опције у Власниковим подешавањума
sidebar_label: White-label власничка подешавања
---

# White-label опције у Власниковим подешавањума

Свака white-label опција коју deployment може да подеси кроз конфигурацију (`appsettings`/env) je **такође
подесива у runtime-у од стране власника апликације**, из **Settings → Deployment**, без поновног deployment-а. Owner
override **побеђује над конфигурацијом**; брисање га враћа опциji на deployment-ово конфигурисано (или
уграђено подразумевано) вредност.

Ово огледа начин на koji white-label *deployment* конфигурише производ — исти knobs, исти ефекат —
тако да оператер може да подешава branding, gates и политику live и види резултат одмах.

## Где живи

- **UI:** власничка само **Deployment** секција у settings дијалогу, и deep-linkable страница
  **`/settings/deployment`**. Опције су груписане у **tab по категориji** (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, ca windowed
  дијалогом на desktop-у и full-screen површином на телефонима.
- **API:** `/api/whitelabel` (само власник, никада feature-gated):
  - `GET /api/whitelabel` — свака опција ca њеном ефективном вредношћу, provenance (`Config` / `Owner` /
    `Default`) и да ли je override постављен. **Тајне су маскиране** (вредност се никада не враћа).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — постави override (валидирано по врсти опције). Празна
    вредност на **тајни** чува постојећу тајну.
  - `DELETE /api/whitelabel/{key}` — обриши један override (врати на config).
  - `POST /api/whitelabel/reset` — обриши **све** override-ове (врати deployment на чисту config).

## Како override-ови ступају на снагу

Owner override-ови се чувају kao шифровано-где-потребно `AppSetting` редови и наслањају се на везане
`AppOptions` од стране декорисаног `IOptionsMonitor<AppOptions>`. Зато што сваки потрошач већ чита опције
преко тог monitor-а, override се примењуje **live преко целе апликације** — тема, наслов странице, MFA
gate, AI-provider gates, broker allow-list, регистрациона политика, email transport подешавања, итд. се ажурирају
на следеће читање (тема/branding се ререндерује одмах). Ако је база података тренутно недоступна,
слој **fail-ује open** на конфигурисани baseline, тако да override читање никада не може да поквари апликацију.

**Feature flags** су део исте површине али се перзистују кроз постојећу feature-override
складиште (`IFeatureGate`), тако да се Features tab и самостални feature toggle-ови никада не разилазе.

**Тајне** (SMTP лозинка, CAPTCHA тајна, provisioning тајна) су шифроване at rest
(`ISecretProtector`, purpose `whitelabel.secret`), write-only у UI, и никада се не враћају од стране API-ja.

## Делегиране опције

**Дељена Open API апликација** креденцијали и **per-message-type rate limits** се управљају на
**Open API** settings секциji (види copy-trading / Open API документацију). Појављују се у Deployment
каталогу kao *делегиране* ставке (read-only овде, ca линком) тако да се ништа не дуплира и sync
гаранција и даље рачуна их kao покривене.

## Увек синхронизовани (спроведено)

Додавање нове white-label опције у конфигурацију **мора** да изложи њен UI у owner settings у истоj
комисиji. Ово je спроведено од стране `WhiteLabelCatalogParityTests`: рефлектује преко сваког white-label
options-record својства и не успева на build-у осим ако својство nije регистровано у
`Core/WhiteLabel/WhiteLabelCatalog` (или експлицитно наведено у `IntentionallyExcluded` ca razлогом).
Види мандат 10 у `CLAUDE.md`.

## Напомене

- Омогућавање SMTP-а на deployment-у koji je започео **без** конфигурисаног email-а треба restart (sender
  тип се бира при покретању); host/креденцијали већ конфигурисаног sender-а се ажурирају live.
- Ознаке/описи опција су технички идентификатори config-knob-а приказани kao подаци; ознаке табова и
  сав интерактивни chrome су потпуно локализовани.
