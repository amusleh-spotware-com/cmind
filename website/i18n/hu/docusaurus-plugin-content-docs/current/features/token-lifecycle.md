---
description: "A cTrader Open API egy érvényes access tokent enged egy cTrader ID (cID) számára egyszerre. Abban a pillanatban, amikor egy új token kibocsátásra kerül — ütemezett frissítés, vagy újra-engedélyezés, amikor a felhasználó egy másik számlát kapcsol ugyanarra a cID-re — az előző access token érvénytelenítve lesz."
---

# Open API token életciklus

A cTrader Open API **egy érvényes access tokent enged egy cTrader ID (cID) számára egyszerre**. Abban a pillanatban, amikor egy új token kibocsátásra kerül — ütemezett frissítés, vagy újra-engedélyezés, amikor a felhasználó egy másik számlát kapcsol ugyanarra a cID-re — az előző access token érvénytelenítve lesz. Egy copy engine, amely egy távoli node-on fut, azt a most halott tokent tartja, így az új tokennak el kell érnie azt a node-ot a élő kapcsolat megszakítása nélkül.

## Modell

- **`OpenApiAuthorization`** az az aggregátum, amely egy cID titkosított access + refresh tokenjeit tárolja. Egyedi index `(UserId, CtidUserId)`-n kényszeríti, hogy **pontosan egy engedély legyen cID-ként per felhasználó**.
- **`TokenVersion`** — egy monoton counter, amely minden token rotációnál növekszik (`Refresh()`, amely lefedi az újra-auth útvonalat is, amikor egy másik számla van linkelve ugyanarra a cID-re). Ez a single-valid-token szabály verzió marker-e, és ez az, amit egy futó host használ a változás detektálására, még ha két token string巧合 is).
- A tokenek titkosítva vannak nyugalomban az `ISecretProtector` révén (`EncryptionPurposes.OpenApiAccessToken` / `OpenApiRefreshToken`). Sose kerülnek logolásra vagy plaintext-ben tárolásra.

## Propagálás (graceful in-place swap)

1. Egy token rotál → az új token + növekedett `TokenVersion` perzisztál.
2. A `CopyEngineSupervisor` a hosting node-on minden reconcile ciklusban újraolvassa a tervet és kiszámít egy **token signature-t** (access tokenek + verziók). Egy változás rotációt jelent.
3. Ahelyett, hogy lebonthatná a host-ot és újraindíthatná (ami leejtené a mester execution stream-jét), a supervisor **push-olja az új tokent a futó host-ba**.
4. A host újra-authentikálja az érintett számlát **a meglévő socket-en** (`ProtoOAAccountAuthReq` újra) a `SwapAccessTokenAsync`-en keresztül, majd egy könnyű reconcile-t végez. A régi token meghal; a copy stream soha nem áll meg.

Ez teszi biztonságossá a cross-cID esetet: egy felhasználó, aki egy második számlát ad hozzá ugyanarra a cID-re futás közben, érvényteleníti a régi tokent, és a futó copy profil az új-on folytatódik.

## Frissítés

Az `OpenApiTokenRefreshService` (háttér) proaktívan frissíti az engedélyeket lejárat előtt; az `OpenApiAuthorization.IsExpiring(threshold, now)` kapuzza. A cTrader a **refresh** token-t rotálja minden frissítéskor, tehát az új refresh token azonnal perzisztál; egy csak-olvasható cache, amely nem tud perzisztálni, önérvénytelenítene (releváns az in-cluster teszt Job-ra, amely egy írható másolatot mountol a secret-ből).

### Hiba eszkaláció

Egy sikertelen frissítés nem csendes. Az `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)` rögzíti a `RefreshFailedAt`-ot, növeli a `ConsecutiveRefreshFailures`-t, és mindig emel egy `AccessTokenRefreshFailed`-et (warning). Amikor a token most az `App:OpenApi:TokenRefreshCriticalWindow` (alapértelmezés 6h) belül van a lejárattól és a frissítés még mindig sikertelen, egyszer **eszkalál** egy `AccessTokenRefreshCritical` domain eseménnyel + `Critical` loggal, így a tulajdonos újra-engedélyezhet, mielőtt a copy/prop-firm műveletek elvesztik a token-t. A hiba counter és az eszkaláció latch-e resetel a következő sikeres `Refresh`-re. A szolgáltatás minden `TokenRefreshInterval`-nél újrapróbálkozik, így egy szolgáltató/karbantartás kiesés ön-gyógyul, amikor a frissítési végpont visszatér.

## Érvénytelenítési riasztás & auto-helyreállítás (M1)

Egy részleges/újra-engedélyezés egy cID-n érvényteleníti azt a token-t, amelyet a futó copy host még tart. Amikor egy kereskedési hívás `OpenApiErrorKind.TokenInvalid`-val reject-ol, a host emel egy külön **`CopyTokenInvalidated`** riasztást (log 1078) — nem egy generikus hiba — így a notification csatorna tudja, hogy egy token figyelmet igényel. A helyreállás automatikus: a supervisor minden ciklusban újraolvassa az engedélyt, és amikor a frissített token megváltoztatja a token signature-t, push-olja a futó host-ba egy **in-place swap**-ért — a másolás folytatódik kézi újra-hozzáadás nélkül. Egy `NotLinkable` profil (token/auth átmenetileg feloldhatatlan) szintén minden supervisor ciklusban újraértékelődik és abban a pillanatban hostolódik, amikor a terve újra épül.

## Host liveness watchdog (M2)

A supervisor figyeli minden hosted profil run task-ját. Ha egy host kilép vagy faultol, miközben a profilja még mindig ehhez a node-hoz van rendelve, a watchdog cancel-eli és **újraindítja** következő ciklusban (log `CopyHostRestarted`), így egy beragadt host ön-gyógyít ahelyett, hogy manuális restart-ot igényelne — és egy profil hibája soha nem akadályozza meg a többieket (per-profile izoláció).

## Tesztek

- **Egység** — `TokenVersion` növekszik `Refresh`-en; a host végez egy in-place swap-et restart nélkül; cross-cID érvénytelenítés swap-el forrás és cél tokeneket; **egy érvénytelenített cél token emeli a `CopyTokenInvalidated`-et és auto-helyreáll a következő token push-on** (M1); a watchdog `IsHostDead` döntése újraindít egy befejezett/faulted host-ot és egy reassigned profil-t egyedül hagy (M2).
- **Integráció** — `TokenVersion` perzisztál + növekszik EF-en valódi Postgres-en; a token signature változik verzió bump-on, még ha a string változatlan is.
