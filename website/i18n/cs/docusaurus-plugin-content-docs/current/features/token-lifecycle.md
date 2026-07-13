---
description: "Open API cTrader umožňuje jeden platný přístupový token na cTrader ID (cID) najednou. V okamžiku, kdy je vydán nový token — plánovaná obnovení nebo znovuautorizace — je předchozí token zneplatněn."
---

# Životní cyklus Open API tokenu

Open API cTrader umožňuje **jeden platný přístupový token na cTrader ID (cID) najednou**. V okamžiku, kdy je vydán nový token — plánovaná obnova nebo znovuautorizace, když uživatel propojí další účet na stejném cID — je předchozí přístupový token zneplatněn. Copy engine běžící na vzdáleném uzlu drží ten nyní mrtvý token, takže nový token k němu musí dorazit bez přerušení živého spojení.

## Model

- **`OpenApiAuthorization`** je agregát, který drží šifrované access + refresh tokeny cID. Unikátní index na `(UserId, CtidUserId)` vynucuje **přesně jednu autorizaci na cID na uživatele**.
- **`TokenVersion`** — monotónní čítač zvýšený při každé rotaci tokenu (`Refresh()`, což také pokrývá cestu znovu-autentifikace, když je na stejném cID propojen další účet). Je to verzační marker pro pravidlo jednoho platného tokenu a to, co běžící hostitel používá k detekci změny, i kdyby dva řetězce tokenů náhodou kolidovaly.
- Tokeny jsou šifrovány v klidu přes `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` / `OpenApiRefreshToken`). Nikdy nejsou logovány ani ukládány v plaintextu.

## Šíření (graceful in-place swap)

1. Token se otočí → nový token + zvýšená `TokenVersion` jsou persistovány.
2. `CopyEngineSupervisor` na hostitelském uzlu znovu čte plán každý reconciliační cyklus a počítá **token signature** (přístupové tokeny + verze). Změna znamená rotaci.
3. Místo rozebrání hostitele a restartu (což by zahodilo execution stream mastera), supervisor **pushuje nový token do běžícího hostitele**.
4. Hostitel znovu autentifikuje dotčený účet **na existujícím socketu** (`ProtoOAAccountAuthReq` znovu) přes `SwapAccessTokenAsync`, pak provede lehkou rekonciliaci. Starý token zemře; copy stream se nikdy nezastaví.

To je to, co dělá cross-cID případ bezpečným: uživatel přidávající druhý účet ze stejného cID uprostřed běhu invaliduje starý token a běžící copy profil pokračuje na novém.

## Obnova

`OpenApiTokenRefreshService` (background) proaktivně obnovuje autorizace před vypršením; `OpenApiAuthorization.IsExpiring(threshold, now)` to řídí. cTrader rotuje **refresh** token při každé obnově, takže nový refresh token je okamžitě persistován; cache pouze pro čtení, která nemůže persistovat, by se invalidovala (relevantní pro in-cluster test Job, který mountuje zapisovatelnou kopii secret).

### Eskalace selhání

Selhaná obnova není tichá. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)` zaznamenává `RefreshFailedAt`, zvyšuje `ConsecutiveRefreshFailures` a vždy vyvolává `AccessTokenRefreshFailed` (warning). Když je token nyní do `App:OpenApi:TokenRefreshCriticalWindow` (default 6h) od vypršení a obnova stále selhává, eskaluje **jednou** s doménovou událostí `AccessTokenRefreshCritical` + `Critical` log, aby owner mohl znovu autorizovat předtím, než copy/prop-firm operace token ztratí. Čítač selhání a eskalační západka se resetují při dalším úspěšném `Refresh`. Služba pokračuje v opakování každých `TokenRefreshInterval`, takže výpadek poskytovatele/maintenance se self-healuje, když se refresh endpoint vrátí.

## Alert na invalidaci & auto-recovery (M1)

Částečná/znovu-autorizace na cID invaliduje token, který běžící copy hostitel stále drží. Když obchodní volání odmítne s `OpenApiErrorKind.TokenInvalid`, hostitel vyvolá distinct **`CopyTokenInvalidated`** alert (log 1078) — ne generické selhání — takže kanál notifikací ví, že token potřebuje pozornost. Recovery je automatická: supervisor znovu čte autorizaci každý cyklus a, když obnovený token změní token signature, pushuje ho do běžícího hostitele pro **in-place swap** — kopírování pokračuje bez manuálního znovupřidání. `NotLinkable` profil (token/auth dočasně nerozpsaný) je stejně znovu vyhodnocován každý supervisor cyklus a hostován v momentě, kdy se jeho plán znovu sestaví.

## Watchdog živosti hostitele (M2)

Supervisor sleduje run task každého hostovaného profilu. Pokud hostitel skončí nebo selže, zatímco jeho profil je stále přiřazen k tomuto uzlu, watchdog zruší a **restartuje** ho příští cyklus (log `CopyHostRestarted`), takže zaseknutý hostitel se self-healuje místo potřeby manuálního restartu — a selhání jednoho profilu nikdy nezastaví ostatní (izolace per-profil).

## Testy

- **Unit** — `TokenVersion` se zvýší na `Refresh`; hostitel provede in-place swap bez restartu; cross-cID invalidace vymění source a destination tokeny; **invalidovaný destination token vyvolá `CopyTokenInvalidated` a auto-recoveruje na další token push** (M1); watchdog rozhodnutí `IsHostDead` restartuje dokončený/selhavší hostitel a nechá reassignovaný profil být (M2).
- **Integration** — `TokenVersion` persistuje + inkrementuje přes EF na skutečném Postgres; token signature se změní při version bump, i když string zůstane nezměněn.
