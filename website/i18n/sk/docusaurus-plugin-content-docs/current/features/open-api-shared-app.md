---
description: "Dodaj jednu cTrader Open API aplikáciu pre každého používateľa (white-label shared mode), jedinú redirect URL na registráciu a per-message-type client rate limits."
---

# Zdieľaná Open API aplikácia & rate limits

Predvolene každý používateľ registruje **vlastnú** cTrader Open API aplikáciu pod
**Settings → Open API**. White-label operátor (typicky cTrader broker alebo reseller) môže namiesto toho
dodať **jednu zdieľanú Open API aplikáciu pre všetkých používateľov** — nikto neregistruje vlastnú; každý
autorizuje svoje účty cez operátorovu jedinú aplikáciu.

## Dva spôsoby ako poskytnúť zdieľanú aplikáciu

Zdieľaná aplikácia sa provisioningbuje buď z deployment config **alebo** z owner settings UI
(owner-set hodnota vyhráva). Poskytnite ju raz a shared-mode sa zapne pre všetkých.

### 1. Deployment konfigurácia (seeding pri štarte)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // kanonická verejná URL TOHTO deploymentu
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // encrypted at rest; nikdy nelogované
    }
  }
}
```

Pri štarte aplikácia seedingne jednu zdieľanú aplikáciu vlastnenú owner účtom (idempotent — nikdy
neprepíše owner-editovanú runtime hodnotu, a re-seeding je no-op).

### 2. Owner settings (runtime, bez redeployu)

**Settings → Open API** (len owner) zobrazuje dve veci: sekcia **Vaša Open API aplikácia** — owner registruje, upravuje a autorizuje svoju **vlastnú** per-user aplikáciu presne ako hocikto iný používateľ (dostupná pokiaľ nie je nakonfigurovaná žiadna shared app) — a **Deployment shared application** card na pridanie / úpravu / vymazanie zdieľanej aplikácie, s redirect URL zobrazenou na copy-paste. Zmeny naberajú účinnosť pre nové autorizácie okamžite. Keď je shared app nakonfigurovaná, superseduje owner vlastnú aplikáciu a sekcia **Vaša Open API aplikácia** sa zmení na upozornenie, že účty sa teraz autorizujú cez shared app.

## Redirect URL (zaregistrujte to v cTrader)

Každá cTrader Open API aplikácia registruje **jednu** redirect URL — **rovnakú jednu hodnotu** pre
zdieľanú aplikáciu aj pre akúkoľvek per-user aplikáciu:

```
{your deployment URL}/openapi/callback
```

napríklad `https://cmind.yourbroker.com/openapi/callback`.

- Aplikácia **zobrazuje presnú hodnotu** na stránke Open API settings (s kopírovacím tlačidlom) — vložte ju
  do cTrader partner portálu keď vytvárate Open API aplikáciu.
- Je zložená z `App:OpenApi:PublicBaseUrl` takže zostáva stabilná za reverse proxy / CDN;
  keď to nie je nastavené, vracia sa k inbound request host.
- Invite vs normálny používateľský experience sa líši len v tom, kde používateľ pristane **po** callbacku
  (zoznam jeho účtov vs potvrdenie "účty pridané") — registrovaná redirect URL sa nezmení.

## Čo používateliavidia v shared mode

Keď zdieľaná aplikácia existuje:

- Používatelia **nemajú možnosť** registrovať vlastnú Open API aplikáciu — settings stránka zobrazuje
  **"Open API je spravované vaším poskytovateľom"** a tlačidlo **Authorize accounts** ktoré používa zdieľanú
  aplikáciu.
- Akékoľvek pre-existujúce osobné aplikácie sú **odstránené**; ich autorizované účty sú prepojené na
  zdieľanú aplikáciu a musia byť **znovu autorizované** (ich staré tokeny boli vydané pod iným client
  id). Pokus o vytvorenie osobnej aplikácie vráti chybu "managed by your provider".

## Client rate limits (per message type)

Client paceuje odchádzajúce cTrader Open API správy tak, aby burst nikdy nevyvolal server-side rate-limit
blok. Limity sú **per message type**, zodpovedajú cTrader Open API dokumentácii:

| Kategória | Čo pokrýva | Predvolené |
|---|---|---|
| `General` | trading + read správy (orders, symbols, account queries) | 45 msg/s |
| `HistoricalData` | trendbar / tick-data požiadavky (cTrader ich throttleuje prísnejšie) | 5 msg/s |

Historical-data požiadavka sa počíta proti **obehám** — jej vlastnej aj general bucket. Heartbeat a
autentizačné správy sa nikdy nepaceujú. Správy sa queued a drainujú dostupnou rýchlosťou — nič nie je
dropnuté a poradie je zachované.

Nastavte ich ak váš broker negotiations **vyššie** cTrader limity, alebo nastavte kategóriu na **`0`** pre
disable pacing úplne (neobmedzené):

- **Config:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msgs/sec).
- **Owner settings:** **Client rate limits** card na **Settings → Open API** (owner override vyhráva,
  aplikuje sa na nové connection / pri reconnect).
