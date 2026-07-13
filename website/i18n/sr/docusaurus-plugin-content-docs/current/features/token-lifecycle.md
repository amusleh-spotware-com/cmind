---
description: "cTrader Open API dozvoljava jednu validnu access token po cTrader ID-u (cID) u jednom trenutku. U trenutku kada se izda novi token — planirano osvežavanje, ili re-authorization kada korisnik linkuje drugi račun na isti cID — prethodni access token se invalidira."
---

# Životni ciklus Open API tokena

cTrader Open API dozvoljava **jednu validnu access token po cTrader ID-u (cID) u jednom trenutku**. U trenutku
kada se izda novi token — planirano osvežavanje, ili re-authorization kada korisnik linkuje drugi
račun na isti cID — prethodni access token se invalidira. Copy engine koji radi na
remote node-u drži taj sada-mrtvi token, tako da novi token mora do njega bez prekidanja
live konekcije.

## Model

- **`OpenApiAuthorization`** je agregat koji drži cID-ov encrypted access + refresh
  tokene. Jedinstveni indeks na `(UserId, CtidUserId)` enforce-uje **tačno jednu autorizaciju po cID-u
  po korisniku**.
- **`TokenVersion`** — monotonic counter koji se bump-uje svaki put kada se token rotira (`Refresh()`,
  što takođe pokriva re-auth put kada je drugi račun linkovan na isti cID). To je
  version marker za single-valid-token pravilo i ono što running host koristi da detektuje promenu čak i ako dva token string-a slučajno koliziraju.
- Tokeni su enkriptovani at rest preko `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`). Nikad se ne loguju niti čuvaju u plaintext-u.

## Propagacija (graceful in-place swap)

1. Token se rotira → novi token + bumped `TokenVersion` se perzistiraju.
2. `CopyEngineSupervisor` na hosting node-u ponovo čita plan svaki reconcile cycle i
   računa **token signature** (access tokeni + verzije). Promena znači rotaciju.
3. Umesto da se host sruši i restartuje (što bi prekinulo master's execution stream), supervisor **gura novi token u running host**.
4. Host se re-authenticates na pogođenom računu **na postojećem socket-u**
   (`ProtoOAAccountAuthReq` ponovo) preko `SwapAccessTokenAsync`, zatim radi light reconcile. Stari token umire; copy stream se nikad ne zaustavlja.

Ovo čini cross-cID slučaj bezbednim: korisnik koji dodaje drugi račun sa istog cID-a
usred run-a invalidira stari token, i running copy profile nastavlja na novom.

## Osvežavanje

`OpenApiTokenRefreshService` (background) proaktivno osvežava autorizacije pre isteka;
`OpenApiAuthorization.IsExpiring(threshold, now)` gating-uje ga. cTrader rotira **refresh** token
pri svakom osvežavanju, tako da se novi refresh token perzistira odmah; read-only cache koji ne može
perzistirati bi se sam invalidirao (relevantno za in-cluster test Job, koji montira writeable copy
secret-a).

### Failure escalation

Neuspešno osvežavanje nije tiho. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
beleži `RefreshFailedAt`, inkrementira `ConsecutiveRefreshFailures`, i uvek podiže
`AccessTokenRefreshFailed` (warning). Kada je token sada unutar `App:OpenApi:TokenRefreshCriticalWindow`
(podrazumevano 6h) od isteka i osvežavanje i dalje ne uspeva, eskalira **jednom** sa
`AccessTokenRefreshCritical` domen događajem + `Critical` log tako da owner može re-authorize pre nego što
copy/prop-firm operacije izgube token. Failure counter i escalation latch se resetuju na sledeći
uspešni `Refresh`. Servis nastavlja da retrie-uje svaki `TokenRefreshInterval`, tako da provider/maintenance
outage self-heal-uje kada refresh endpoint vrati.

## Invalidacijski alert i auto-recovery (M1)

Delimična/again-authorization na cID invalidira token koji running copy host još drži. Kada trading poziv
reject-uje sa `OpenApiErrorKind.TokenInvalid`, host podiže distinkt
**`CopyTokenInvalidated`** alert (log 1078) — ne generički neuspeh — tako da notifikacioni kanal zna da
token treba pažnju. Recovery je automatski: supervisor ponovo čita autorizaciju svaki cycle i,
kada osveženi token promeni token signature, gura ga u running host za **in-place
swap** — kopiranje se nastavlja bez manuelnog re-add-a. `NotLinkable` profil (token/auth privremeno
ne-resolvable) se takođe re-evaluira svaki supervisor cycle i hostuje u trenutku kada se njegov plan ponovo izgradi.

## Host liveness watchdog (M2)

Supervisor gleda svaki hosted profilov run task. Ako host izađe ili fault-uje dok je njen profil još
dodeljen ovom node-u, watchdog otkazuje i **restartuje** ga sledeći cycle (log
`CopyHostRestarted`), tako da zaglajeni host self-heal-uje umesto da zahteva manuelni restart — i jedan profilov
neuspeh nikad ne blokira druge (per-profile izolacija).

## Testovi

- **Unit** — `TokenVersion` bump-uje na `Refresh`; host izvršava in-place swap bez restarta;
  cross-cID invalidacija swapa source i destination tokene; **invalidiran destination token podiže
  `CopyTokenInvalidated` i auto-recover-uje se na sledeći token push** (M1); watchdog `IsHostDead`
  odluka restartuje završeni/faulted host i ostavlja reassigned profil sam (M2).
- **Integration** — `TokenVersion` perzistira + inkrementira kroz EF na realnom Postgres-u; token
  signature se menja na version bump čak i ako je string nepromenjen.
