---
description: "Rebrand dell'app per rivenditore — nome del prodotto, logo, favicon, colori, CSS personalizzato — tramite config di deployment, nessun cambio di codice. Ogni valore di branding predefinisce su identità stock…"
---

# White-label branding

Rebrand dell'app per rivenditore — nome del prodotto, logo, favicon, colori, CSS personalizzato — tramite config di deployment, nessun cambio di codice. Ogni valore di branding **predefinisce su identità stock**: deployment non configurato assomiglia a prima; il rivenditore sovrascrive solo ciò che serve.

## Modello

- `Core.Options.BrandingOptions` — associato da `App:Branding`. String-based (edge di config); ogni colore validato quando il tema è costruito.
- `Core.Branding.HexColor` — oggetto di valore per colore CSS hex (`#RGB` / `#RRGGBB`), immutabile, auto-validante.
  Colore non valido lancia `DomainException` (`domain.branding.color_invalid`) quando il tema è costruito — deployment non configurato fallisce veloce all'avvio, non renderizza una tavolozza rotta.
- `Web.Components.Theme.Build(BrandingOptions)` — produce tema MudBlazor dal branding. Solo le voci di tavolozza branded vengono da config; tipografia, layout, tonalità di superficie neutra rimangono fisse così il prodotto mantiene un aspetto coerente tra rivenditori.
- `Web.Branding.IBrandingThemeProvider` — singleton, costruisce il tema una volta, ricostruisce al cambio di opzioni.
  Iniettato da `MainLayout`/`EmptyLayout` per `MudThemeProvider`, dalla barra dell'app per nome/logo del prodotto. `App.razor` legge `IOptionsMonitor<AppOptions>` direttamente per `<head>` della pagina (titolo, descrizione, favicon, theme-colour, CSS personalizzato).

## Configurazione

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — copy trading e automazione di strategia.",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "AppBarColor": "#0B1220",
      "BackgroundColor": "#0E1525",
      "SurfaceColor": "#161E30",
      "SuccessColor": "#3FB950",
      "ErrorColor": "#F85149",
      "WarningColor": "#D29922",
      "InfoColor": "#2D7FF9",
      "CustomCss": ".mud-appbar { letter-spacing: 1px; }"
    }
  }
}
```

Forma di variabile d'ambiente: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Chiave | Effetto | Predefinito |
|-----|--------|---------|
| `ProductName` | Testo app-bar + page `<title>` | `cMind` |
| `LogoUrl` | Immagine logo app-bar; quando vuoto, il testo del nome del prodotto mostra | *(vuoto)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | descrizione stock |
| `PrimaryColor` / `SecondaryColor` | accento, icona drawer, pulsanti | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + superfici; `AppBarColor` guida `<meta theme-color>` + PWA manifest `theme_color`, `BackgroundColor` il manifest `background_color` | tavolozza scura |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | colori di stato | stock |
| `CustomCss` | `<style>` iniettato in `<head>` (deployment-trusted) | *(vuoto)* |
| `ShowSiteLink` | mostra il link di credito "Powered by cMind" sulla dashboard | `true` |
| `RequireMfa` | richiedi a ogni utente di impostare l'autenticazione a due fattori prima di usare l'app | `false` |
| `NodesUi` | quanto della superficie Nodes viene spedita: `Full` (lista + add/delete manuale), `Monitor` (lista di sola lettura, no add/delete), `Hidden` (no nav, no page, no API manuale) | `Full` |
| `RestrictNodesToOwner` | quando `true`, solo il proprietario può vedere/gestire i nodi; altrimenti l'intera superficie di staff admin-o-superior può. Gli utenti normali non vedono mai i nodi in entrambi i casi | `false` |

Asset referenziati da `LogoUrl`/`FaviconUrl` serviti da Web app `wwwroot` (ad esempio montare cartella `wwwroot/branding/`) o qualsiasi URL assoluto.

`App:Branding` validato all'avvio (`BrandingOptionsValidator`, eseguito tramite `ValidateOnStart`): ogni colore deve essere hex valido, `CustomCss` non deve contenere `<`/`>` (non può scappare dal tag `<style>`). Deployment non configurato fallisce al boot con messaggio chiaro, non renderizza la pagina rotta.

## Link Powered-by

La dashboard renderizza un piccolo link di credito **"Powered by cMind"** che punta al sito di documentazione del progetto. È controllato da `App:Branding:ShowSiteLink` ed è **`true` per impostazione predefinita** — un deployment non configurato lo mostra. Un rivenditore che esegue un'istanza completamente white-labeled imposta `App__Branding__ShowSiteLink=false` per rimuoverlo completamente.

Il link è emesso dal componente della dashboard e legge il flag tramite `IBrandingThemeProvider` / `BrandingOptions`, quindi attivare/disattivarlo è un cambio di sola config (nessuna ricompilazione). Vedi [White-label per business](../white-label-for-business.md#the-powered-by-cmind-link) per il riepilogo rivolto al business.

## Broker allowlist

Un deployment white-label può limitare quali account di trading dei broker i suoi utenti possono aggiungere — così un broker che esegue cMind solo per i suoi client serve solo il suo libro. Configurato sotto `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Forma di variabile d'ambiente: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Comportamento:**

- **Lista vuota (predefinito) ⇒ senza restrizioni.** Ogni broker è consentito e **nessuna verifica viene eseguita** — un deployment stock è completamente inalterato.
- **Non-vuoto ⇒ ristretto.** cMind controlla ogni account che un utente tenta di aggiungere contro la lista (case-insensitive):
  - **Open API (OAuth) link** — il nome del broker è riportato autoritativamente da cTrader Open API, quindi un account non consentito viene semplicemente **saltato** (gli account consentiti nella stessa sovvenzione si collegano comunque); la pagina di autorizzazione dice all'utente quali broker sono stati saltati.
  - **cID manuale (nome utente / password)** — il broker digitato dall'utente **non** è attendibile. cMind **verifica** il broker reale dell'account eseguendo il probe broker spedito cBot tramite CLI cTrader (leggendo `Account.BrokerName`) e persiste quel nome verificato. Un broker non consentito è rifiutato con una notifica; un fallimento di verifica (credenziali errate, nessun nodo, timeout) è anche mostrato, e l'account non è aggiunto.

**Modello:**

- `Core.Options.AccountsOptions` — associato da `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`, `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — oggetto di valore (trimmed, uguaglianza case-insensitive).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; vuoto = consenti tutti. Applicato come invariante dentro `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount` (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — esegue il container probe sull'host web (che ha il socket Docker), tails logs, e analizza il broker tramite `Core.Accounts.BrokerProbeOutput`. Invocato solo quando l'allowlist è ristretto.

**Broker-probe cBot:** un `broker-probe.algo` precostruito viene spedito con l'app Web (`src/Web/BrokerProbe/`, copiato nell'output come `broker-probe/broker-probe.algo`), quindi il predefinito `App:Accounts:BrokerProbeAlgoPath` risolve out of the box — un percorso relativo è risolto contro la directory di base dell'app, un percorso assoluto è usato come dato. La sorgente vive in `tools/broker-probe/`. Quando l'algo è assente, la verifica manuale di cID fallisce chiusa — gli account sotto un allowlist ristretto possono ancora essere collegati tramite il percorso Open API, che non ha bisogno di probe.

## Broker allowlist — test

- **Unità** — `UnitTests/Accounts/`: oggetti di valore `BrokerName`/`BrokerAllowlist`, parser `BrokerProbeOutput`, e l'invariante allowlist `CTraderIdAccount`.
- **Integrazione** — `IntegrationTests/BrokerAllowlistTests.cs`: endpoint cID manuale con un verificatore fake (senza restrizioni / verificato / non consentito / verifica-fallita) + linker Open API che salta gli account non consentiti. `BrokerVerifierLiveTests.cs` esegue il probe **reale** quando le credenziali cID + l'algo sono forniti (salta pulitamente altrimenti).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: un deployment ristretto rifiuta un'aggiunta manuale tramite l'UI reale e mostra la notifica "couldn't verify" (nessuna riga di account aggiunta).

## Visibilità UI dei nodi

I nodi sono infrastruttura che la maggior parte dei tenant non gestisce mai manualmente — gli agenti cTrader CLI [si auto-registrano e heartbeat](../operations/node-discovery.md), quindi un deployment white-label può nascondere i controlli manuali, o la superficie Nodes interamente, e ancora eseguire un cluster sano tramite auto-discovery. Due chiavi di branding di sola config governano questo:

```json
{
  "App": {
    "Branding": {
      "NodesUi": "Monitor",
      "RestrictNodesToOwner": true
    }
  }
}
```

Forma di variabile d'ambiente: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — tre modalità:**

- **`Full` (predefinito)** — il prodotto stock: la lista dei nodi più i controlli manuali **New Node** e **Delete**. `POST`/`DELETE /api/nodes` funzionano.
- **`Monitor`** — una superficie di sola lettura: la lista e le statistiche live rimangono, ma l'aggiunta manuale e l'eliminazione sono rimosse. I nodi appaiono solo tramite auto-discovery. `POST`/`DELETE /api/nodes` restituiscono **404**.
- **`Hidden`** — il link di navigazione Nodes e la pagina sono completamente scomparsi e il percorso della pagina reindirizza alla dashboard; l'API di aggiunta/eliminazione manuale è disattivata. Il cluster è solo auto-discovery.

**`RestrictNodesToOwner`** fissa chi può vedere e gestire i nodi. Predefinito `false` mantiene la superficie di staff **admin-o-superior** standard (`AdminOrAbove`); impostare `true` per renderla **solo-proprietario** (`Owner`). In entrambi i casi gli **utenti normali non vedono mai i nodi** — questo sceglie solo tra proprietario-solo e la superficie di staff più ampia.

L'**auto-discovery dei nodi è inalterato da entrambe le chiavi**: l'endpoint anonimo `POST /api/nodes/register` self-register + heartbeat funziona sempre, quindi un deployment `Hidden`/`Monitor` cresce ancora il suo cluster automaticamente.

**Modello:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — l'unica fonte di verità che compone la modalità + restrizione del proprietario: `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav (`NavMenu.razor`), la pagina (`Pages/Nodes.razor`) e gli endpoint (`NodeEndpoints`) la leggono così l'UI e l'API non possono mai non concordare.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — associato da `App:Branding`.

## Visibilità UI dei nodi — test

- **Unità** — `UnitTests/Nodes/NodesUiAccessTests.cs`: risoluzione di visibilità pagina, gestione manuale e politica richiesta su ogni modalità + branding predefinito.
- **Integrazione** — `IntegrationTests/NodeUiGatingTests.cs`: su HTTP reale + Postgres — `Full` consente un'aggiunta manuale, `Monitor`/`Hidden` 404 add e delete, e `RestrictNodesToOwner` vieta un admin mentre il proprietario legge ancora la lista.
- **E2E** — `E2ETests/NodesUiTests.cs` (predefinito `Full`: link di navigazione + pagina + pulsante New Node renderizzano) e `E2ETests/NodesHiddenTests.cs` (`Hidden`: link di navigazione scomparso, `/nodes` reindirizza).

## Token di design (variabili CSS)

Il branding raggiunge anche il **proprio** foglio di stile dell'app + componenti personalizzati, non solo MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emette la tavolozza branded come proprietà personalizzate CSS su `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), iniettate in `App.razor` subito dopo `site.css`. `site.css` e ogni componente leggono `var(--app-*)` — **nessun colore hard-coded** — così la tavolozza di un rivenditore scorre ovunque (hero login, nav inferiore, suggerimenti di aiuto, pagina offline) gratuitamente. Le tonalità di superficie neutra predefiniscono in `site.css :root`; `CustomCss` (iniettato ultimo) può sovrascrivere qualsiasi token. Vedi [ui-guidelines.md](../ui-guidelines.md) §2.

## PWA brandizzato

L'app installabile è anche brandizzata — l'endpoint del manifesto (`/manifest.webmanifest`) è costruito da `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → tema/sfondo). Vedi [pwa.md](pwa.md).

## Test

- **Unità** — `UnitTests/Branding/HexColorTests.cs`: validazione hex valida/non valida.
- **Integrazione** — `IntegrationTests/ThemeBuildTests.cs`: i colori mappano nella tavolozza, il colore non valido lancia; `IntegrationTests/BrandingHttpTests.cs`: custom `ProductName`/description/theme-colour rendono nella `<head>` della pagina servita (WebApplicationFactory + Postgres), i predefiniti mantengono il nome stock.
- **E2E** — `E2ETests/BrandingTests.cs`: il nome del prodotto branded renderizza nella barra dell'app in un browser reale.
