---
description: "Open API cTraderja omogoča en veljaven dostopovni žeton na cTrader ID (cID). V trenutku, ko se izda nov žeton — načrtovano osvežitev ali pa…"
---

# Življenjski cikel žetona Open API

Open API cTraderja omogoča **en veljaven dostopovni žeton na cTrader ID (cID)**. V trenutku, ko se izda nov žeton — načrtovana osvežitev ali pa
ponovna avtorizacija, ko uporabnik poveže drug račun na istem cID — se prejšnji dostopovni žeton razveljavi. Podvajalni pogon, ki teče na oddaljenem vozlišču, še vedno drži ta zdaj mrtvi žeton, zato mora nov žeton do njega priti, ne da bi prekinil živo povezavo.

## Model

- **`OpenApiAuthorization`** je agregat, ki hrani šifrirane dostopovne in osveževalne žetone cID. Enoličen indeks na `(UserId, CtidUserId)` vsiljuje **natančno eno avtorizacijo na cID na uporabnika**.
- **`TokenVersion`** — monotono števec, ki se poveča vsakič, ko se žeton zavrti (`Refresh()`,
  kar prav tako pokriva pot ponovne avtentikacije, ko je na istem cID povezan drug račun). Je
  oznaka različice za pravilo enega veljavnega žetona in tisto, kar tekoči gostitelj uporablja za zaznavanje
  spremembe, četudi bi se zgodila kolizija dveh žetonskih nizov.
- Žetoni so šifrirani pri miru prek `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`). Nikoli se ne beležijo ali shranjujejo v navadnem besedilu.

## Širjenje (gladka zamenjava na mestu)

1. Žeton se zavrti → nov žeton + povečana `TokenVersion` sta vztrajna.
2. `CopyEngineSupervisor` na gostiteljskem vozlišču ob vsakem ciklu usklajevanja znova prebere načrt in
   izračuna **podpis žetona** (dostopovni žetoni + različice). Sprememba pomeni rotacijo.
3. Namesto da bi podrl gostitelja in ponovno zagnal (kar bi prekinilo glavni pretok izvajanja),
   supervisor **potisne nov žeton v tekočega gostitelja**.
4. Gostitelj ponovno avtenticira prizadeti račun **na obstoječem vtiču**
   (`ProtoOAAccountAuthReq` znova) prek `SwapAccessTokenAsync`, nato pa izvede lahkotno uskladitev.
   Stari žeton umre; pretok kopiranja se nikoli ne ustavi.

To naredi primeren cross-cID primer varnega: uporabnik, ki med delovanjem doda drugi račun iz istega cID,
razveljavi stari žeton, in tekoče podvajalno profil nadaljuje z novim.

## Osveževanje

`OpenApiTokenRefreshService` (ozadje) proaktivno osvežuje avtorizacije pred potekom;
`OpenApiAuthorization.IsExpiring(threshold, now)` jih nadzoruje. cTrader zavrti **osveževalni** žeton
ob vsakem osveževanju, zato se nov osveževalni žeton takoj vztraja; predpomnilnik samo za branje, ki ne more
vztrajati, bi se sam razveljavil (relevantno za testno opravilo v gruči, ki namesti pisno kopijo
skrivnosti).

### Eskalacija napak

Neuspelo osveževanje ni tiho. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
zabeleži `RefreshFailedAt`, poveča `ConsecutiveRefreshFailures` in vedno dvigne
`AccessTokenRefreshFailed` (opozorilo). Ko je žeton zdaj znotraj `App:OpenApi:TokenRefreshCriticalWindow`
(privzeto 6 ur) poteka in osveževanje še vedno propada, eskalira **enkrat** z
`AccessTokenRefreshCritical` domen dogodek + `Critical` dnevnik, tako da lahko lastnik ponovno avtorizira, preden
kopiraj/prop-firm operacije izgubijo žeton. Števec napak in eskalacijska zaskočnica se ponastavita na naslednje
uspešno `Refresh`. Storitev še naprej poskuša na vsak `TokenRefreshInterval`, torej izpad ponudnika/maintenance
samozdravi, ko osvežitvena končna točka spet deluje.

## Opozorilo o razveljavitvi in samodejno okrevanje (M1)

Delna/ponovna avtorizacija na cID razveljavi žeton, ki ga še vedno drži tekoči podvajalni gostitelj. Ko
trgovalni klic zavrne z `OpenApiErrorKind.TokenInvalid`, gostitelj dvigne ločeno
**`CopyTokenInvalidated`** opozorilo (dnevnik 1078) — ne generično napako — tako da kanal za obvestila ve, da
žeton potrebuje pozornost. Okrevanje je samodejno: supervisor znova prebere avtorizacijo vsak cikel in,
ko sveži žeton spremeni podpis žetona, ga potisne v tekočega gostitelja za **zamenjavo na mestu**
— kopiranje se nadaljuje brez ročnega ponovnega dodajanja. Profil `NotLinkable` (žeton/auth začasno
nerazrešljiv) se prav tako znova ovrednoti vsak cikel supervisorja in se gosti v trenutku, ko se njegov načrt spet zgradi.

## Pas pasovnega watchdoga gostitelja (M2)

Supervisor spremlja opravilo tekočega profila vsakega gostitelja. Če gostitelj zapusti ali okvari, medtem ko je njegov profil
še vedno dodeljen temu vozlišču, watchdog prekliče in **ponovno zažene** naslednji cikel (dnevnik
`CopyHostRestarted`), torej zataknjen gostitelj samozdravi namesto da bi potreboval ročni ponovni zagon — in en napaki profil
nikoli ne ustavi drugih (izolacija na profil).

## Testi

- **Enote** — `TokenVersion` se poveča pri `Refresh`; gostitelj izvede zamenjavo na mestu brez ponovnega zagona;
  cross-cID razveljavitev zamenja vire in cilje žetonov; **razveljavljen ciljni žeton dvigne
  `CopyTokenInvalidated` in samodejno okreva na naslednjem potisku žetona** (M1); odločitev watchdoga `IsHostDead`
  ponovno zažene končan/napčan gostitelj in pusti ponovno dodeljenemu profilu pri miru (M2).
- **Integracija** — `TokenVersion` vztraja + poveča skozi EF na resničnem Postgres; podpis žetona
  se spremeni pri povečanju različice, četudi je niz nespremenjen.
