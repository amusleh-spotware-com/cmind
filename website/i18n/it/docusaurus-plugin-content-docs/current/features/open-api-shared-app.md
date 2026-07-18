---
description: "Spedire una applicazione Open API cTrader per ogni utente (white-label shared mode), il single redirect URL da registrare, e rate limit client per tipo messaggio."
---

# Shared Open API application & rate limit

Di default ogni utente registra la **propria** applicazione cTrader Open API sotto
**Settings → Open API**. Un operatore white-label (tipicamente un broker o rivenditore cTrader) può invece
spedire **una applicazione Open API condivisa per tutti gli utenti** — nessuno registra la propria; tutti
autorizzano i loro account attraverso la singola app dell'operatore.

## Due modi per fornire l'applicazione condivisa

L'applicazione condivisa è provisioned da deployment config **oppure** dalla UI owner settings
(il valore owner ha priorità). Forniscine una e la shared-mode si attiva per tutti.

### 1. Deployment config (seeded all'avvio)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // URL pubblico canonico di QUESTO deployment
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // crittografato at rest; mai loggato
    }
  }
}
```

All'avvio l'app seed una applicazione condivisa owned dall'account owner (idempotent — non sovrascrive
mai un valore runtime edited dall'owner, e re-seeding è un no-op).

### 2. Owner settings (runtime, no redeploy)

**Settings → Open API** (solo owner) mostra due cose: una sezione **Your Open API application** — il proprietario registra, modifica e autorizza la **propria** app per-user esattamente come qualsiasi utente (disponibile mentre nessuna app condivisa è configurata) — e una card **Deployment shared application** per aggiungere / modificare / eliminare l'app condivisa, con l'redirect URL displayed per copy-paste. I cambiamenti hanno effetto per le nuove autorizzazioni immediatamente. Una volta che un'app condivisa è configurata, sostituisce l'app personale del proprietario, e la sezione **Your Open API application** passa a un avviso che gli account ora autorizzano attraverso l'app condivisa.

## L'redirect URL (registralo in cTrader)

Ogni applicazione cTrader Open API registra **un** redirect URL — lo **stesso singolo valore** per la
shared app e per qualsiasi app per-user:

```
{your deployment URL}/openapi/callback
```

per esempio `https://cmind.yourbroker.com/openapi/callback`.

- L'app **visualizza il valore esatto** sulla pagina Open API settings (con un pulsante copia) — incollalo
  nel portale partner cTrader quando crei l'applicazione Open API.
- È composto da `App:OpenApi:PublicBaseUrl` così resta stabile dietro un reverse proxy / CDN;
  quando non è impostato cade back all'host della richiesta inbound.
- L'experience invite vs utente normale differisce solo dove l'utente atterra **dopo** il callback
  (la loro lista account vs una conferma "accounts added") — l'redirect URL registrato è invariato.

## Cosa vedono gli utenti sotto shared mode

Quando esiste un'applicazione condivisa:

- Gli utenti **non hanno opzione** per registrare la propria applicazione Open API — la pagina settings mostra
  **"Open API è gestita dal tuo provider"** e un pulsante **Authorize accounts** che usa l'app condivisa.
- Qualsiasi applicazione personale pre-esistente viene **rimossa**; i loro account autorizzati sono
  re-pointed all'app condivisa e devono essere **re-authorized** (i loro vecchi token erano emessi sotto
  un diverso client id). Tentare di creare un'app personale restituisce un errore "managed by your provider".

## Client rate limit (per tipo messaggio)

Il client dosa i messaggi outbound cTrader Open API così un burst non fa mai scattare un blocco rate-limit
lato server. I limiti sono **per tipo messaggio**, matching i cTrader Open API docs:

| Category | Cosa copre | Default |
|---|---|---|
| `General` | trading + messaggi read (ordini, simboli, account query) | 45 msg/s |
| `HistoricalData` | richieste trendbar / tick-data (throttled harder da cTrader) | 5 msg/s |

Una richiesta historical-data conta contro **entrambi** il suo bucket e quello general. I messaggi heartbeat e
authentication non sono mai paced. I messaggi queue e drain all'available rate — niente dropped e l'ordine è
preservato.

Configurali se il tuo broker ha negoziato limiti cTrader **più alti**, oppure imposta una categoria a **`0`**
per disabilitare il pacing interamente (unlimited):

- **Config:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msgs/sec).
- **Owner settings:** la card **Client rate limits** su **Settings → Open API** (owner override ha
  priorità, si applica a nuove connessioni / on reconnect).
