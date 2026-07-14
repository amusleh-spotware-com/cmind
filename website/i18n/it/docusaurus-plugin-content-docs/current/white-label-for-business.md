---
slug: /white-label-for-business
title: White-label per business
description: Spedisci cMind come il tuo prodotto branded — per prop firm, broker e servizi di copy-trading. Rebrand ogni superficie tramite config, nessun cambio di codice.
sidebar_position: 4
---

# cMind white-label per il tuo business 🏢

Gestisci una prop firm, una scrivania di brokeraggio, o un servizio di copy-trading? cMind è stato costruito da zero per essere
**rivenduto come il tuo prodotto**. Ogni superficie — il nome, il logo, il favicon, i colori, anche
l'app del telefono installabile — si piega al tuo marchio. I tuoi clienti vedono l'azienda *tua*. Nessun cambio di codice,
nessun fork, solo config.

:::tip[TL;DR]
Punta `App:Branding` al tuo nome, colori e logo. Riavvia. Fatto. Il riferimento tecnico completo vive
nella [documentazione della feature white-label](./features/white-label.md).
:::

## Cosa puoi rebrandizzare

| Superficie | Cosa cambia |
|---|---|
| **Nome del prodotto** | Testo app bar + titolo scheda browser |
| **Logo & favicon** | I tuoi marchi ovunque, inclusa la scheda del browser |
| **Colori** | Tavolozza completa — primario, superfici, colori di stato — scorre per tutta l'UI *e* il CSS della propria app tramite token di design |
| **App installabile (PWA)** | Il nome add-to-home-screen, l'icona e lo splash usano il tuo marchio |
| **Meta / SEO** | Descrizione e URL di supporto sono tuoi |
| **CSS personalizzato** | Inietta il tuo stesso polish per l'ultimo 5% |

Tutto predefinisce all'identità stock di cMind, così sovrascrivi solo ciò che ti interessa.

## Il rebrand di 60 secondi

Imposta questi nel tuo deployment (config JSON o variabili d'ambiente):

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

Forma di variabile d'ambiente: `App__Branding__ProductName=AcmeFX`. I colori sono validati all'avvio —
un valore hex errato fallisce il boot con un messaggio chiaro invece di renderizzare una pagina rotta. Bello e
forte, esattamente quando lo vuoi.

## Il link "Powered by cMind"

Per **impostazione predefinita**, la dashboard mostra un piccolo e raffinato link **"Powered by cMind"** che
punta i visitatori a questo sito. È attivato per impostazione predefinita perché siamo orgogliosi del progetto e
aiuta altri trader a trovarlo — ma è **la tua scelta**.

- **Tienilo** (predefinito): un link di credito sottile sulla dashboard. Non ti costa nulla, aiuta il progetto.
- **Nascondilo**: imposta `App__Branding__ShowSiteLink=false` e scompare del tutto — perfetto per un
  deployment completamente white-labeled dove il prodotto è inconfondibilmente il *tuo*.

Vedi la [documentazione della feature white-label](./features/white-label.md#powered-by-link) per esattamente dove
viene renderizzato.

## Branding multi-tenant per-customer

Poiché il branding è solo config di deployment, ogni deployment di tenant può portare la sua identità. Esegui un'istanza
separata per cliente, o guida il branding dal tuo piano di controllo — l'app lo legge da
`IOptionsMonitor`, quindi può anche ricostruire il tema live quando le opzioni cambiano.

Abbina questo con:

- **[Feature toggles](./features/feature-toggles.md)** — decidi quali capacità ogni tenant vede.
- **[Regole prop-firm](./features/prop-firm.md)** — applica le tue regole di sfida con tracciamento di equity live.
- **[Commissioni di performance](./features/copy-performance-fees.md)** + **[marketplace di provider](./features/copy-provider-marketplace.md)** — monetizza il copy trading.
- **[Compliance](./features/compliance.md)** — mantieni l'audit trail che il tuo regolatore chiederà.

## Asset e hosting

Metti il tuo logo/favicon nella cartella `wwwroot/branding/` dell'app Web (o punta `LogoUrl`/`FaviconUrl`
a qualsiasi URL assoluto). Distribuisci come meglio credi — [Docker](./deployment/local.md),
[Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md), o
[AWS](./deployment/cloud-aws.md).

Pronto a renderlo tuo? Inizia con il [riferimento tecnico white-label →](./features/white-label.md)
