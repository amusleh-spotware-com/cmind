---
slug: /for-brokers
title: cMind per broker cTrader
description: Perché un broker cTrader dovrebbe eseguire un cMind white-label per i suoi client — dai ai trader IA, copy trading e sfide prop-firm sotto il tuo marchio, limita gli account al tuo brokerage, e vinci un vantaggio competitivo sui concorrenti.
keywords:
  - broker cTrader
  - piattaforma di trading white-label
  - tecnologia broker
  - copy trading per broker
  - strumenti di trading IA
  - software prop firm
sidebar_position: 6
---

# cMind per broker cTrader 🏦

Gestisci un brokerage cTrader. I tuoi client possono già fare trading — ma così possono i client di ogni altro broker.
**cMind ti consente di dare ai tuoi trader una piattaforma completa di operazioni di trading potenziata da IA, con marchio come
la tua**, così costruiscono, backtestano, eseguono, copiano e monitorano strategie dentro il *tuo* ecosistema
invece di andare alla deriva verso uno strumento di terze parti. Questo è più "appiccicaticcio" i client, più volume, e un vero vantaggio
sui broker che offrono solo un terminale.

:::tip TL;DR
Esegui un cMind white-label per i tuoi client. Limita gli account al **tuo** brokerage, attiva IA e
copy trading, e spediscilo sotto il tuo marchio. → [White-label per business](./white-label-for-business.md)
:::

## Il vantaggio che ottieni su altri broker

- **Differenziati sugli strumenti, non solo su spread.** Dai ai client generazione cBot con IA, backtesting su un
  cluster gestito, copy trading, e sfide prop-firm — capacità che la maggior parte dei broker semplicemente non
  offre.
- **Mantieni i client nel tuo ecosistema.** Quando i trader costruiscono ed eseguono le loro strategie dentro la tua piattaforma
  branded, rimangono. La retention è l'intero gioco.
- **Sotto il tuo marchio, nel tuo dominio.** Nome, logo, colori, favicon, anche l'app del telefono installabile —
  tutto tuo. Nessuno vede "cMind." → [Feature white-label](./features/white-label.md)

## Servi solo i tuoi account (broker allowlist)

Eseguendo un white-label per i *tuoi* client? Limita quali account di trading dei broker gli utenti possono aggiungere così
il tuo deployment serve solo il tuo libro:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Il Nome del Tuo Brokerage"]
    }
  }
}
```

Quando l'allowlist è impostato, cMind controlla ogni account che un utente tenta di aggiungere — sia tramite cTrader Open
API che tramite login cID manuale (verificato leggendo il nome del broker reale dell'account) — e rifiuta qualsiasi
account che non sia nella tua lista. Lascialo vuoto e ogni broker è consentito (predefinito). Vedi la
[documentazione della feature white-label](./features/white-label.md#broker-allowlist) per la meccanica completa.

## Spedisci una Open API app per tutti i tuoi utenti

Salta il fastidio per-utente: fornisci **una sola applicazione cTrader Open API** e ogni client autorizza
i suoi account attraverso di essa — nessun client registra mai la propria. Registra un singolo URL di reindirizzamento, elimina
le credenziali in config o nelle impostazioni del proprietario, e la modalità condivisa si attiva per tutti. Hai negoziato un
limite di messaggi cTrader più alto? Sintonizza i **limiti di tasso del client per-tipo di messaggio** (o disabilita il pacing).
→ [Applicazione Open API condivisa e limiti di tasso](./features/open-api-shared-app.md)

## Nuovi modi per monetizzare

- **IA, con zero attrito per i client.** Fornisci una chiave di provider IA predefinita a livello di deployment e
  ogni client ottiene funzionalità IA istantaneamente — nessuna registrazione altrove. Ricaricalo, o raggruppalo nei livelli premium.
  I client possono comunque portare la propria chiave. → [Feature IA](./features/ai.md)
- **Sfide prop-firm.** Esegui sfide di trader finanziati con tracciamento di equity live e regole applicate,
  e addebita per le iscrizioni. → [Regole prop-firm](./features/prop-firm.md)
- **Business di copy-trading.** Le commissioni di performance e un marketplace dei provider trasformano il copy trading in
  ricavi. → [Commissioni di performance](./features/copy-performance-fees.md) ·
  [Marketplace di provider](./features/copy-provider-marketplace.md)
- **Livelli di feature.** Decidi quali capacità ogni segmento di client vede con
  [feature toggles](./features/feature-toggles.md).

## Regolamentato, verificabile, multi-tenant

- **[Compliance](./features/compliance.md)** i log ti danno l'audit trail che il tuo regolatore chiederà.
- **[Autenticazione a due fattori](./features/two-factor-auth.md)** può essere resa obbligatoria per deployment.
- **Branding per-client** — esegui un'istanza separata con brand per segmento, guidata dal tuo piano di controllo.
  → [Branding multi-tenant per-customer](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Come iniziare

1. Leggi [White-label per business](./white-label-for-business.md) per il rebrand di 60 secondi.
2. Imposta `App:Accounts:AllowedBrokers` al tuo brokerage e scegli il tuo [feature set](./features/feature-toggles.md).
3. [Distribuisci](./deployment/cloud.md) — Docker, Kubernetes, Azure, o AWS.

Non vuoi eseguire l'infrastruttura tu stesso? Un provider di hosting può gestire un cMind gestito per te
— indirizzali a [Per provider cloud e VPS](./for-cloud-providers.md).

## Forma la roadmap

cMind è open source. I broker che lo costruiscono ottengono una voce outsized nel suo percorso — richiedi le
integrazioni e i controlli di cui hai bisogno, e contribuisci indietro tramite la
[Guida Contribuente](./contributing.md).
