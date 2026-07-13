---
title: Fejlesztői hitelesítési adatok
description: "A cMind E2E es integrációs tesztek authentikáltak egy valódi cTrader Open API alkalmazással - a dev creds-titkosított, teljes hozzáférésű, és soha nincs benne a gitben."
---

# Fejlesztői hitelesítési adatok

A cMind E2E és integrációs tesztek authentikáltak egy valódi cTrader Open API alkalmazással - a dev creds titkosított, teljes hozzáférésű, és soha nincs benne a gitben.

## Architektúra

```
secrets/
  openapi-dev.local.json   ← developer credentials (NEM in git)
  openapi-prod.local.json  ← production credentials (NEM in git)
```

Mindkettő ugyanúgy van titkosítva (`ISecretProtector`, `EncryptionPurposes.OpenApiCredentials`) mint az éles titkok. A `.local.json` kiterjesztés a `.gitignore`-ban van.

## Tartalom

```json
{
  "clientId": "...",
  "clientSecret": "...",
  "redirectUrl": "http://localhost:5000/openapi/callback"
}
```

A `clientId` + `clientSecret` a cTrader Developer Portal-on regisztrált alkalmazásból van. A `redirectUrl` megegyezik a `App:OpenApi:PublicBaseUrl`-gal + `/openapi/callback`.

## Beszerzés

1. Menj a <https://developers.ctrader.com>-re és regisztrálj egy alkalmazást (vagy használd a meglévő dev alkalmazásodat).
2. Add hozzá a `redirectUrl`-t: `http://localhost:5000/openapi/callback` (fejlesztői Aspire stack) vagy `http://localhost:8080/openapi/callback` (csak Web).
3. Másold ki a `clientId` + `clientSecret`-et.
4. Hozd létre / frissítsd a `secrets/openapi-dev.local.json`-t.

## Használat

Az Alkalmazásbetöltő (`AppLoader`) automatikusan betölti az `openapi-dev.local.json`-t, ha létezik és a `App:OpenApi:ClientId` nincs beállítva. Ez lehetővé teszi a tesztek számára, hogy éles OAuth folyamatot hajtsanak végre teljes jogkörrel.

Az E2E tesztek `AiLocalFixture` és `CopyTradingLiveTests` is ebből a fájlból olvasnak, ha a `COPY_SECRET` env var nincs beállítva.

## Biztonság

- A titkok soha nincsenek a gitben (`.gitignore` + `*.local.json`).
- Az E2E tesztek csak olvasási műveleteket végeznek (számlainformációk lekérése, másolási profilok listázása).
- A titkosított tárolás ugyanazt a `ISecretProtector`-t használja, mint az éles titkok.

## Titrálás

Ha a dev alkalmazás tokenje lejár (refresh token single-use, 30 napos élettartam):

1. Töröld a `secrets/openapi-dev.local.json` `accessToken` / `refreshToken` mezőjét.
2. Futtasd újra a tesztet - az Alkalmazásbetöltő új OAuth folyamattal regenerálja.
3. A fájl automatikusan frissül az új titkokkal.
