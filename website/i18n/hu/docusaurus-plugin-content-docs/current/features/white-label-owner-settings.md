---
id: white-label-owner-settings
title: White-label opciók a Tulajdonosi beállításokban
sidebar_label: White-label tulajdonosi beállítások
---

# White-label opciók a Tulajdonosi beállításokban

Minden white-label opció, amelyet egy telepítés a konfiguráción keresztül beállíthat (`appsettings`/env), **szintén futásidőben beállítható az alkalmazás tulajdonosa által**, a **Beállítások → Deployment**-ből, újra-deploy nélkül. Egy tulajdonosi felülírás **felülírja a konfigurációt**; törlése visszaállítja az opciót a telepítés konfigurált (vagy beépített alapértelmezett) értékére.

Ez tükrözi, hogyan konfigurál egy white-label *telepítés* a terméket — ugyanazok a gombok, ugyanaz a hatás — így egy operátor élőben hangolhatja a brandinget, gate-eket és policy-t és azonnal látja az eredményt.

## Hol él

- **UI:** a tulajdonos-kizárólagos **Deployment** szekció a beállítások dialogban, és a deep-linkelhető oldal **`/settings/deployment`**. Az opciók **kategoriánként tab-okba** csoportosítva (Branding, Téma, Funkciók, Regisztráció, Számlák, Email, AI, Open API, Prop firm), mobil-először, asztali gépen ablakos dialog-gal és telefonon teljes-képernyős felületen.
- **API:** `/api/whitelabel` (csak tulajdonos, soha nem feature-gated):
  - `GET /api/whitelabel` — minden opció az effektív értékével, proveniencia (`Config` / `Owner` / `Default`) és hogy egy felülírás be van-e állítva. **A titkok maszkolva** (az érték sose kerül visszaadásra).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — állíts be egy felülírást (validálva opció fajta szerint). Egy üres érték egy **titkon** megtartja a meglévő titkot.
  - `DELETE /api/whitelabel/{key}` — törölj egy felülírást (állj vissza a konfigurációra).
  - `POST /api/whitelabel/reset` — törölj **minden** felülírást (állj vissza a tiszta konfigurációra).

## Hogyan lépnek hatályba a felülírások

A tulajdonosi felülírások titkosított-ahol-kell `AppSetting` sorokként tárolódnak és rétegezve vannak a kötött `AppOptions` tetejére egy díszített `IOptionsMonitor<AppOptions>` által. Mivel minden consumer már ezen a monitoron keresztül olvassa az opciókat, egy felülírás **élőben** érvényesül a teljes alkalmazásban — a téma, oldal cím, MFA gate, AI-szolgáltató gate-ek, bróker allow-list, regisztrációs policy, email transport beállítások stb. a következő olvasásra frissülnek (a téma/branding azonnal újrarajzol). Ha az adatbázis rövid ideig nem elérhető, a réteg **fail-open**-re a konfigurált baseline-ra, így egy felülírás olvasás soha nem törheti meg az alkalmazást.

**A feature flag-ek** ugyanannak a felületnek a részei, de a meglévő feature-override tárolón keresztül perzisztálódnak (`IFeatureGate`), így a Features tab és az önálló feature toggle-ök soha nem divergálnak.

**A titkok** (SMTP jelszó, CAPTCHA titok, provisioning titok) titkosítva vannak nyugalomban (`ISecretProtector`, purpose `whitelabel.secret`), write-only az UI-ban, és sose kerülnek visszaadásra az API által.

## Delegált opciók

A **megosztott Open API alkalmazás** hitelesítő adatai és **per-üzenet-típusú rate limitek** az **Open API** beállítások szekcióban kezelhetők (lásd a copy-trading / Open API dokumentumokat). Megjelennek a Deployment katalógusában *delegált* bejegyzésekként (itt csak olvasható, egy linkkel), így semmi nincs duplikálva és a szinkron garancia továbbra is számítja őket fedettként.

## Mindig szinkronban (kényszerítve)

Egy új white-label opció hozzáadása a konfigurációhoz **ugyanabban a commitban** ki kell hogy hozza a tulajdonosi beállításokban. Ezt a `WhiteLabelCatalogParityTests` kényszeríti: reflektál minden white-label options-record property-n és fail-ol a build-en, hacsak a property nincs regisztrálva a `Core/WhiteLabel/WhiteLabelCatalog`-ban (vagy explicite felsorolva `IntentionallyExcluded`-ban egy okkal).
Lásd a 10. mandátumot a `CLAUDE.md`-ben.

## Megjegyzések

- Az SMTP bekapcsolása egy olyan telepítésen, amely **nincs** email konfigurálva indult, restart-ot igényel (a sender típus az indításkor választódik); a már konfigurált sender host/hitelesítő adatai élőben frissülnek.
- Az opció **címkék/leírások** technikai config-knob azonosítók, amelyek adatként vannak mutatva; a tab címkék és minden interaktív chrome teljesen lokalizált.
