---
description: "Ladji eno cTrader Open API aplikacijo za vsakega uporabnika (white-label skupna način), enojno redirect URL za registracijo, in omejitve hitrosti na tip sporočila na strani odjemalca."
---

# Skupna Open API aplikacija in omejitve hitrosti

Privzeto vsak uporabnik registrira svojo **lastno** cTrader Open API aplikacijo pod
**Settings → Open API**. White-label operater (tipično cTrader broker ali preprodajalec) lahko namesto tega
ladi **eno skupno Open API aplikacijo za vse uporabnike** — nihče ne registrira svoje; vsi
avtorizirajo svoje račune prek operaterjeve enojne aplikacije.

## Dva načina za določitev skupne aplikacije

Skupna aplikacija je določena bodisi iz namestitvene konfiguracije **ali** iz nastavitev lastnika UI
(lastnik-setvena vrednost zmaga). Določite jo enkrat in skupni način se vključi za vse.

### 1. Namestitvena konfiguracija (sejano ob zagonu)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // kanonična javna URL TE NAMESTITVE
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // šifrirano pri miru; nikoli logirano
    }
  }
}
```

Ob zagonu aplikacija seje eno skupno aplikacijo v lasti lastniškega računa (idempotentno — nikoli ne
prepiše lastnikovo-urejene runtime vrednosti, in re-seeding je brez-učinka).

### 2. Lastnik nastavitve (runtime, brez ponovnega uvajanja)

**Settings → Open API** (samo lastnik) prikazuje dve stvari: sekcijo **Vaša Open API aplikacija** — lastnik registrira, ureja in avtorizira svojo **lastno** per-user aplikacijo točno kot kateri koli uporabnik (razpoložljivo medtem ko ni konfigurirana skupna aplikacija) — in **Deployment shared application** kartico za dodajanje / urejanje / brisanje skupne aplikacije, z redirect URL prikazano za copy-paste. Spremembe učinkujejo za nove avtorizacije takoj. Ko je skupna aplikacija konfigurirana, nadomesti lastnikovo lastno aplikacijo, in sekcija **Vaša Open API aplikacija** se preslika v obvestilo, da se računi sedaj avtorizirajo prek skupne aplikacije.

## Redirect URL (to registrirajte v cTrader)

Vsaka cTrader Open API aplikacija registrira **eno** redirect URL — **isto enojno vrednost** za
skupno aplikacijo in za katerokoli per-user aplikacijo:

```
{your deployment URL}/openapi/callback
```

na primer `https://cmind.yourbroker.com/openapi/callback`.

- Aplikacija **prikaže natančno vrednost** na strani Open API nastavitev (s kopirnim gumbom) — prilepite jo
  v cTrader partnerski portal ko ustvarjate Open API aplikacijo.
- Sestavljena je iz `App:OpenApi:PublicBaseUrl` tako da ostane stabilna za reverse proxy / CDN;
  ko je to nenastavljeno, pade nazaj na inbound request host.
- Izkušnja vabiji proti normalnemu uporabniku se razlikuje samo v tem kam uporabnik pristane **po** callbacku
  (njihov seznam računov proti potrditvi "računi dodani") — registriran redirect URL je nespremenjen.

## Kaj uporabniki vidijo v skupnem načinu

Ko skupna aplikacija obstaja:

- Uporabniki **nimajo možnosti** za registracijo lastne Open API aplikacije — stran nastavitev prikazuje
  **"Open API upravlja vaš ponudnik"** in gumb **Avtoriziraj račune** ki uporablja skupno aplikacijo.
- Katerokoli obstoječe osebne aplikacije so **odstranjene**; njihovi avtorizirani računi so preusmerjeni na
  skupno aplikacijo in morajo biti **ponovno avtorizirani** (njihovi stari žetoni so bili izdani pod drugo
  id odjemalca). Poskus ustvarjanja osebne aplikacije vrne napako "upravlja vaš ponudnik".

## Omejitve hitrosti na strani odjemalca (na tip sporočila)

Odjemalec tempirajo izhodna cTrader Open API sporočila tako da sunek nikoli ne sproži strežnik-side
rate-limit blokade. Omejitve so **na tip sporočila**, ujemanje s cTrader Open API dokumentacijo:

| Kategorija | Kaj pokriva | Privzeto |
|---|---|---|
| `General` | trgovalna + bralna sporočila (naročila, simboli, poizvedbe računa) | 45 msg/s |
| `HistoricalData` | zahtevki trendbar/tick-podatkov (bolj throttleano s strani cTrader) | 5 msg/s |

Zahtevek zgodovinskih podatkov šteje proti **obema** svojemu vedru in splošnemu vedru. Heartbeat in
avtentikacijska sporočila nikoli niso tempirana. Sporočila se vrste in praznijo po razpoložljivi hitrosti — nič
ni izpuščeno in vrstni red je ohranjen.

Nastavite jih če je vaš broker pogajal za **višje** cTrader limite, ali nastavite kategorijo na **`0`** da
onemogočite tempiranje popolnoma (neomejeno):

- **Config:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msgs/sec).
- **Lastnik nastavitve:** kartica **Client rate limits** na **Settings → Open API** (prevzem lastnika,
  učinkuje za nove povezave / ob ponovni povezavi).
