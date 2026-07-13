---
description: "cTrader Open API umožňuje jeden platný prístupový token na cTrader ID (cID) naraz. V momente, keď je vydaný nový token — plánovaná obnova, alebo…"
---

# Open API token životný cyklus

cTrader Open API umožňuje **jeden platný prístupový token na cTrader ID (cID) v čase**. V momente
nový token je vydaný — plánovaná obnova alebo re-autorizácia keď používateľ viazaný iný
účet na rovnaký cID — predchádzajúci token je neplatný. A copy engine spúšťajúce na
vzdialený uzol drží toho teraz-mŕtvého token, takže nový token musí dosiahnuť to bez vypadnutia
live pripojenie.

## Model

- **`OpenApiAuthorization`** je agregát, ktorý drží cID šifrovaný prístup + refresh
  tokeny. Jedinečný index na `(UserId, CtidUserId)` vynúti **presne jeden autorizácia na cID
  na užívateľa**.
- **`TokenVersion`** — monotónny počítadlo udlúšeného pokaždé token sa rotuje (`Refresh()`,
  ktorý tiež pokrýva re-auth cestu keď iný účet je viazaný na rovnaký cID). To je
  verzii značka na pravidlo single-valid-token a je čo bežiacu hostitela používa na detekciu a
  zmeny, dokonca aj keď dva token reťazce náhodou zrážajú.
- Tokeny sú šifrované v pokoji cez `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`). Oni sú nikdy protokolovaní alebo uložené v plaintext.

## Propagácia (milostivý in-place swap)

1. Token sa rotuje → nový token + bumped `TokenVersion` sú trvalé.
2. `CopyEngineSupervisor` na hostovej uzle čítajú plán každý rekoncilácia cyklus a
   počítajú **token podpis** (prístupové tokeny + verzií). Zmena znamená rotáciu.
3. Namiesto rozdelenia dole hostitele a restartovanie (čo by zhodilo master vykonávanie
   stream), supervisor **tlači nový token k bežiacemu hostiteľu**.
4. Host re-authenticates postihnutý účet **na existujúcej zásuvke**
   (`ProtoOAAccountAuthReq` znova) cez `SwapAccessTokenAsync`, potom robí ľahký rekoncilácia. A
   starý token zomrie; kopírovanie stream nikdy sa nezastaví.

Toto je čo robí cross-cID prípad bezpečný: a používateľ pridávajúce druhý účet z rovnakého cID
mid-run neplatný starý token a bežiacu copy profil udržiava sa na novom.

## Refresh

`OpenApiTokenRefreshService` (background) proaktívne obnovuje autorizácie pred vypršaním;
`OpenApiAuthorization.IsExpiring(threshold, now)` brány to. cTrader rotuje **refresh** token
na každý refresh, takže nový refresh token je trvalé okamžite; a read-only cache, ktorý nemôže
trvalé by sebou-zneplatniť (relevantný na in-cluster test Job, ktorý montáž a zapisovateľný kópie
z tajomstva).

### Zlyhanie eskalácii

A zlyhavý refresh nie je ticho. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
záznam `RefreshFailedAt`, inkrementá `ConsecutiveRefreshFailures` a vždy zvyšuje
`AccessTokenRefreshFailed` (upozornenie). Keď token je teraz vnútri `App:OpenApi:TokenRefreshCriticalWindow`
(štandardne 6h) z vypršania a refresh je stále zlyhajúc, to eskaluje **raz** s a
`AccessTokenRefreshCritical` doménová udalosť + `Critical` log, takže vlastník môžu re-authorize pred
kopírovanie/prop-firm operácie stratu token. Počítadlo zlyhania a eskalácii zámka reset na ďalšie
úspešný `Refresh`. Služba naďalej opakujú každý `TokenRefreshInterval`, takže a poskytovať/údržba
výpadok samoreparácia keď obnov koncový bod vracia.

## Nevalidácia alert & auto-recovery (M1)

A čiastočný/znova-autorizácia na cID neplatný token a bežiacu copy hostitela stále drží. Keď a
obchodné volať odmietne s `OpenApiErrorKind.TokenInvalid`, host zvyšuje sa odlišný
**`CopyTokenInvalidated`** upozornenie (log 1078) — nie generický zlyhanie — takže notifikácia kanálu vie a
token potreby pozornosti. Recovery je automatický: supervisor znova-čítajú autorizácia každý cyklus a,
keď osviežené token zmení token podpis, to tlačí do bežiacim hostitele na **in-place
swap** — kopírovanie resumes bez ručného re-pridať. A `NotLinkable` profil (token/auth dočasne
neriešiteľný) je podobne re-vyhodnotené každý supervisor cyklus a hostované momente Its plán stavy znova.

## Host liveness watchdog (M2)

Supervisor sleduje každý hostovaný profil beží úlohu. Ak a hostitele výstupy alebo zlyhá kým Its profil je
stále pridelené tomuto uzol, watchdog zruší a **restartuje** ho ďalšie cyklus (log
`CopyHostRestarted`), takže a wedged hostiteľu samoreparácia namiesto potreby ručného reštartu — a jeden profil
zlyhanie nikdy stanov iný (per-profile izolácia).

## Testy

- **Jednotka** — `TokenVersion` bumps na `Refresh`; hostitelu vykonáva in-place swap bez reštartu;
  cross-cID nevalidácia swaps zdroj a destinácia tokeny; **a nevalidovaný destinácia token zvyšuje
  `CopyTokenInvalidated` a auto-obnovuje na ďalšie token push** (M1); watchdog `IsHostDead`
  rozhodnutie restartuje hotový/zlyhavý hostitela a nechá preradenú profil sám (M2).
- **Integrácia** — `TokenVersion` trvalé + inkrementá cez EF na reálny Postgres; token
  podpis zmeny na verzii bump dokonca aj keď reťazec je nezmenená.
