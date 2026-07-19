---
description: "cMind KI ist anbieterunabhaengig – Anthropic, OpenAI, Azure OpenAI, Google Gemini und jeden OpenAI-kompatiblen Endpunkt einschliesslich lokaler Modelle (Ollama, LM Studio, vLLM). Waehle einen Anbieter, ein Modell und einen Endpunkt; jede KI-Funktion funktioniert unveraendert."
---

# KI-Funktionen

cMinds KI-Schicht ist **anbieterunabhaengig**. Jede Funktion verwendet eine einzige anbieterneutrale Nahtstelle
(`IAiClient.CompleteAsync`); ein **Routing-Client** loest die aktive Anbieterberechtigung auf und dispatcht
zum passenden Wire-Adapter. Du waehlst einen Anbieter + Modell + Endpunkt (und, wenn der Anbieter es benoetigt,
einen Schluessel); jede bestehende Funktion funktioniert unveraendert mit derselben Steuerung, Verschluesselung, Resilienz und
Degradierung.

**Enthalten:** eine **integrierte lokale LLM ist im Lieferumfang der App enthalten und standardmaessig aktiviert**
(Microsoft.ML.OnnxRuntimeGenAI, z.B. Phi-3-mini) – damit hat jede Bereitstellung funktionierende KI **ohne API-Schluessel
und ohne externen Dienst**. Eine White-Label-Bereitstellung kann sie entfernen und einschraenken, welche Anbieter Benutzer hinzufuegen
duerfen. Darueber hinaus kann jeder externe Anbieter verbunden werden.

Unterstuetzte Anbieter:

- **Integrierte lokale KI** (`BuiltInOnnx`) – In-Process-ONNX-GenAI-Modell, kein Schluessel, mitgeliefert + standardmaessig an.
- **Anthropic** (Claude – Messages API)
- **OpenAI** und **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Jeder OpenAI-kompatible Endpunkt**, einschliesslich **lokaler Modelle** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) und OpenAI-kompatibler Clouds (**Kimi / Moonshot** unter
  `https://api.moonshot.ai/v1/`, OpenRouter, Groq, Together, Mistral, DeepSeek) – alle ueber den
  OpenAI-kompatiblen Adapter, unterschiedlich nur durch Basis-URL + Modell + Schluessel. Der Dialog zum Hinzufuegen des Anbieters bietet
  **Ein-Klick-Voreinstellungen** (Kimi, OpenAI, OpenRouter, Groq, DeepSeek, Mistral, Ollama, LM Studio), die die
  Basis-URL + ein Beispielmodell ausfuellen.

Genau **ein** Anbieter ist zu einem Zeitpunkt aktiv. Berechtigungen werden **verschluesselt** gespeichert
(`AiProviderCredential` Aggregate + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
ein lokaler Endpunkt braucht **keinen Schluessel**. Ohne aktiven Anbieter gibt jede Funktion das deaktivierte
Ergebnis zurueck und der Rest der App laeuft unveraendert weiter (kein Schluessel noetig zum Erstellen, Testen oder Betreiben der Plattform).

**Rueckwaertskompatibilitaet:** Eine vorhandene Bereitstellung legacy `App:Ai:ApiKey` (oder die alte verschluesselte `ai.api_key`
Einstellung) wird automatisch als Standard-**Anthropic**-Anbieter geehrt – keine Aktion erforderlich.

KI nicht konfiguriert → KI-Seiten dimmen Aktionen und zeigen ein Banner sowie eine Einmal-Aufforderung zum Hinzufuegen eines Anbieters in
**Einstellungen → KI** (`AiFeatureNotice`). Status unter `GET /api/ai/status` (`{ enabled, kind, model }`);
Anbieter verwaltet (nur Eigentuemer) ueber `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}`, und ein `POST /api/ai/providers/test` Konnektivitaets-Ping.

## Bereitstellungsstandard vs. eigener Anbieter eines Benutzers

KI-Berechtigungen haben zwei Umfaenge:

- **Bereitstellungsstandard (eigentuemerverwaltet).** Der Eigentuemer konfiguriert einen Anbieter (oder liefert einen ueber
  `App:Ai:Providers[]` / den Legacy-`App:Ai:ApiKey`). Er wird zur **freigegebenen Standardeinstellung fuer jeden Benutzer** –
  damit kann ein Broker oder Hosting-Anbieter KI fuer alle seine Benutzer finanzieren mit **keiner Einrichtung pro Benutzer und ohne
  pro-Benutzer-Limit**. Verwaltet ueber die nur-Eigentuemer-`/api/ai/providers`-Routen oben.
- **Eigener Anbieter eines Benutzers (Self-Service).** Jeder angemeldete Benutzer darf seinen eigenen Anbieter unter
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}` hinzufuegen. Wenn vorhanden, ueberschreibt sein **eigener aktiver Anbieter den Bereitstellungsstandard
  fuer seine eigenen KI-Funktionen**; das Entfernen kehrt zum Standard zurueck.

**Aufloesungsreihenfolge** (in `AiProviderStore`, pro Benutzer der Anfrage): die eigene aktive Berechtigung des Benutzers →
den Bereitstellungsstandard → den Legacy-Konfigurationsschluessel → keiner (KI deaktiviert). Genau eine Berechtigung ist aktiv
**pro Umfang** (ein partieller eindeutiger Index pro `OwnerUserId`), und jeder Umfang wird unabhaengig aufgeloest, damit ein
Benutzer, der seinen eigenen Schluessel aktiviert, nie den freigegebenen Standard beeintraechtigt. Hintergrund/Nicht-Web-Kontexte (kein Anfragebenutzer)
loesen immer den Bereitstellungsstandard auf.

## Anbieter-Faehigkeitsmatrix

Faehigkeiten sind standardmaessig pro Anbieter und vom Eigentuemer ueberschreibbar. Wenn eine Faehigkeit deaktiviert ist, **degradiert** die Funktion, wirft aber nie: Websuche wird leise verworfen; Vision gibt ein typisiertes
Faehigkeit-nicht-unterstuetzt-Fehler zurueck.

| Anbieter | Art | Standard-Basis-URL | Schluessel erforderlich | Websuche | Vision | Notizen |
|---|---|---|---|---|---|---|
| Integrierte lokale KI | `BuiltInOnnx` | n/v (In-Process) | nein | ✖ | ✖ | mitgeliefertes ONNX-GenAI-Modell, standardmaessig an |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | ja | ✅ | ✅ | Messages API, `web_search`-Tool |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | ja | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | ja | ✅ | ✅ | Bereitstellungspfad + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | ja | ✅ | ✅ | `generateContent`, `google_search`-Verankerung |
| Ollama (lokal) | `OpenAiCompatible` | `http://localhost:11434/v1/` | nein | ✖ | modellabhaengig | ueber OpenAI-kompatiblen Adapter |
| LM Studio (lokal) | `OpenAiCompatible` | `http://localhost:1234/v1/` | nein | modellabhaengig | modellabhaengig | ueber OpenAI-kompatiblen Adapter |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | deine bediente URL | nein | ✖ | modellabhaengig | ueber OpenAI-kompatiblen Adapter |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | Anbieter-URL | ja | ✖ | modellabhaengig | ueber OpenAI-kompatiblen Adapter |

Vollstaendige anbieterspezifische Einrichtungsanleitungen (Schluessel, URLs, Modell-IDs, UI-Schritte): siehe
[KI-Anbieter – Einrichtungskatalog](../deployment/ai-providers.md).

## Integrierte lokale KI (mitgeliefert, standardmaessig an)

cMind liefert eine **echte lokale LLM, die In-Process laeuft** ueber
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (ein kompaktes Instruct-Modell wie
Phi-3-mini). Sie braucht **keinen API-Schluessel und keinen externen Dienst**, und beim ersten Start – wenn kein Anbieter
konfiguriert ist und das White-Label-Gate dies erlaubt – wird sie **automatisch gesaeut und aktiviert**, damit jede
Bereitstellung standardmaessig funktionierende KI hat.

- Das Modellverzeichnis (`genai_config.json` + Tokenizer + Gewichte) wird durch
  `App:Ai:BuiltIn:ModelPath` konfiguriert (Standard `models/onnx`, relativ zum App-Basisverzeichnis). Wenn die Modell-
  dateien fehlen, **degradiert der Anbieter zu einem typisierten Fehler mit einem Installationshinweis** – er wirft nie,
  und der Rest der App ist nicht betroffen.
- Sie betreibt jede Text-KI-Funktion. Da es sich um ein kompaktes Modell handelt, ist es text-only (keine serverseitige Websuche oder
  Vision) und die Generierung ist serialisiert (eine Modellinstanz, wiederverwendet nach Lazy Load).
- **Mehrere integrierte Modelle koennen koexistieren.** Jedes heruntergeladene Modell befindet sich unter `ModelPath/<key>`; ein kurierter Katalog (Phi-3-mini Standard, plus Phi-3-mini-128k) kann heruntergeladen und aus **Einstellungen → KI** gewechselt werden. Das Auswaehlen eines integrierten Submodells laedt es im Prozess. Modell beschaffen/paketieren: see [KI-Anbieter → integriert](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## White-Label-Steuerungen

Eine White-Label-Bereitstellung schraenkt KI ueber `App:Branding` ein (serverseitig durchgesetzt bei jedem Anbieter-Upsert):

- `AllowBuiltInAi` (Standard `true`) – setze `false`, um **das integrierte Modell vollstaendig zu entfernen**.
- `AllowLocalProviders` (Standard `true`) – setze `false`, um lokale/self-gehostete Endpunkte zu verbieten (Loopback /
  private OpenAI-kompatibel, z.B. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (Standard leer = alle) – liste nur die Arten, die die Bereitstellung sanktioniert (z.B.
  `["Anthropic","OpenAiCompatible"]`), um einzuschraenken, welche Anbieter Benutzer hinzufuegen duerfen.
- `AllowAiModelManagement` (Standard `true`) – setze `false`, um **Modell-Browsing**, die **Pro-Seite-Modellauswahl** und **Pro-Funktion-Modellbindung** zu verbergen. Alle sind vom Eigentuemer zur Laufzeit aus **Einstellungen → Bereitstellung** abstimmbar (live ueber `IOptionsMonitor` ueberlagert) und im `WhiteLabelCatalog` katalogisiert.

## Erweiterung: zukuenftige integrierte Modelle

Die KI-Schicht ist **adapterbasiert und zum Wachsen gebaut**. Jeder Anbieter ist ein `IAiProvider`, ausgewaehlt durch
`AiProviderKind`; die funktionsseitige Nahtstelle (`IAiClient`/`AiFeatureService`) aendert sich nie. Ein neues
integriertes Modell-Runtime hinzufuegen (ein weiteres ONNX-Modell, eine andere In-Process-Engine, GGUF/llama.cpp
In-Proc, etc.) ist eine lokalisierte Aenderung: ein `AiProviderKind` hinzufuegen, einen `IAiProvider`-Adapter implementieren,
registrieren, und (optional) Standard-Saet- + Dialogoption verdrahten – keine Funktions-, Endpunkt- oder MCP-Tool-
Aenderungen. Der integrierte ONNX-Anbieter ist die Referenzimplementierung dieses Musters.

## Faehigkeiten

- **cBot erstellen** – ein projektbasierter Workshop unter `/ai/build`: **erstelle einen neuen cBot** (eindeutiger Name + Sprache) oder **verbessere einen vorhandenen**, der Quellcode hat, dann **chatten** Sie mit einem Modell auf `/ai/build/{projectId}`, um seinen Code zu schreiben und zu verfeinern. **Jede Eingabeaufforderung und Modellantwort wird mit Zeitstempeln gespeichert** und ueberlebt Navigation/Neuladung; der Quellcode des Modells wird auf jeder Umdrehung auf das Projekt angewendet. **Erstellen** und **Ausfuehren** Sie den cBot von derselben Seite aus (oder oeffnen Sie ihn im vollstaendigen Editor). Jedes Projekt wird in der Liste mit seiner **letzten Aenderungszeit** und Steuerelemente zum Anzeigen/Loeschen angezeigt.
- **Pro-Seite-Modellauswahl** – jede KI-Funktionsseite und jedes Dialog zeigen eine **Modellauswahl**, die die Modelle auflistet, die du verwenden kannst (deine eigenen Anbieter + die Bereitstellungsstandards). Sie waehlt vorab die gespeicherte Bindung der Funktion aus, falls gesetzt, sonst das **Standard**-Modell, und das ausgewaehlte Modell wird auf diese eine Aktion angewendet (gesendet als `?modelId=` und von `RoutingAiClient` fuer diesen Aufruf erzwungen). Verborgen, wenn die Bereitstellung Modellverwaltung deaktiviert.
- **Modelle durchsuchen und auswaehlen, pro Funktion** – durchsuche die Modelle, die ein Anbieter-Endpunkt bewirbt (`GET /v1/models` auf LM Studio / Ollama / vLLM / llama.cpp, oder den integrierten Katalog), anstatt manuell eine ID einzugeben, und **binde jede KI-Funktion an ein anderes Modell**, damit mehrere Modelle gleichzeitig verschiedene Funktionen bedienen (eine ungebundene Funktion faellt auf den Standard-Anbieter des Umfangs zurueck).
- **Parameteroptimierung** – geschlossene Schleife: KI schlaegt Parametersaetze vor, jeder persistiert + ueber Nodes getestet (`optimize-run` / `optimize-params`).
- **Autonomer Portfolio-Agent** – mandategetriebene Vorschlaege mit vollstaendigem Entscheidungstagebuch (`AgentMandate` → `AgentProposal`).
- **Agierender Risikowächter** – `AiRiskGuard`-Hintergrunddienst bewertet laufende Bots, kann kritische Risiken **auto-stoppen** (Opt-in).
- **Prop-Firm-Exposure-Wächter** – Drawdown-/Exposure-Limits mit Auto-Flatten.
- **Markt-Alarme** – `AlertRule`-Engine mit KI-Stimmung (Websuche-verankert, wo der Anbieter dies unterstuetzt).
- **Analyse** – cBot-Review, Backtest-Analyse, Post-Mortems, Marktstimmung, Chart-Vision-Design, Marketplace-Kuratierung.

## Oberflaechen

- Web-Endpunkte unter `/api/ai/*` (das KI-Build-Chat `build/{id}/prompt` + `build/{id}/messages`, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …). Jeder Feature-Endpunkt akzeptiert ein optionales `?modelId=<credential>`, um diesen einen Aufruf auf einem ausgewaehlten Modell auszufuehren. Plus **Modell-Entdeckung** (`/api/ai/models/probe`, `/api/ai/usable-models`) und **Pro-Funktion-Bindungen** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`). Die cBot-Projekte, Build und Ausfuehrung verwenden die Builder-Endpunkte erneut (`/api/builder/projects…`).
- MCP-Tools (`AiTools`) fuer KI-Clients – siehe [mcp.md](mcp.md). Anbieter-Auswahl ist fuer MCP-Clients transparent.
- **KI**-Navigationsgruppe – eine Blazor-**Seite pro Funktion**: cBot erstellen (`/ai/build`), Review (`/ai/review`), Debate (`/ai/debate`), Marktstimmung (`/ai/sentiment`), Exposure-Pruefung (`/ai/exposure`), Portfolio-Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimieren (`/ai/optimize`), zuzueglich Portfolio-Agent, Alarme, MCP-Schluessel. Seiten teilen `AiFeaturePageBase` + `AiOutputPanel` + eine `AiModelSelect`; jede zeigt `AiFeatureNotice`, wenn kein Anbieter konfiguriert ist.
- **Einstellungen → KI** (`/settings/ai`, nur Eigentuemer) – Anbieterliste mit einem **Hinzufuegen/Bearbeiten-Anbieter-Dialog** (Art, Basis-URL mit pro-Art-Hinweisen und **Ein-Klick-Voreinstellungen** inkl. Kimi/Moonshot, Ollama und LM Studio, Modell, optionaler Schluessel, Faehigkeitsschalter, "aktiv setzen") und einem **Verbindung testen**-Button.

## Konfiguration

`App:Ai` unterstuetzt sowohl den Legacy-Einzelschluessel als auch die Multi-Anbieter-Saetung:

- Legacy: `ApiKey`, `Model` (Standard `claude-opus-4-8`), `BaseUrl`, `MaxTokens` – werden noch als
  Standard-Anthropic-Anbieter geehrt.
- Multi-Anbieter: `ActiveProvider` (Art) und `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) – werden beim Startup in den Store importiert, wenn noch keine Berechtigungen vorhanden sind, damit ein
  Ops-Team eine konfigurierte (einschliesslich lokaler-LLM) Bereitstellung rein ueber appsettings/env ausliefern kann.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` unveraendert. Fuer Tests/Dev lebt ein Konfigurationsschluessel
in der vereinheitlichten [Dev-Credentials-Datei](../testing/dev-credentials.md) unter `Ai`.

## Zuverlaessigkeit

Der Anbieter wird als unzuverlaessig behandelt – nichts, was er tut, kann die App zum Absturz bringen. Dies gilt identisch
 fuer Cloud- und lokale Endpunkte (ein ausgefallener Ollama wiederholt dann degradiert genau wie ein gedrosselter Anthropic):

- **Graceful Degradation.** Jeder Fehlermodus (kein Anbieter, HTTP 4xx/5xx/429, Timeout, malformed Body,
  leerer Inhalt, nicht unterstuetzte Faehigkeit) gibt ein typisiertes `AiResult.Fail(reason)` zurueck – der Client wirft nie
  in eine Seite, MCP-Tool oder gehosteten Dienst.
- **Resilienz-Pipeline.** `AddAiHttpClient` gibt dem einen gemeinsamen AI-`HttpClient` ein begrenztes Retry bei
  transienten 5xx-/Netzwerkfehlern (exponentielles Backoff + Jitter) plus grosszuegigen Per-Attempt- und Gesamt-
  Timeouts (`AiHttp`), wiederverwendet von jedem Adapter.

## Testen mit dem Fake Local LLM

Die KI-Schicht wird Ende-zu-Ende **ohne externe Abhaengigkeit** durch `FakeLocalLlmServer` bewiesen — ein kleiner
In-Process-**OpenAI-kompatibler** Endpunkt, der eine deterministische Konservenantwort zurueckgibt, drahtidentisch zu
Ollama/LM Studio/vLLM. Er stuetzt:

- **Unit** — Adapter-Anfrage-Uebersetzung + Antwort-Parsing-Tests, Routing/Faehigkeitsdegradierung.
- **Integration** — der OpenAI-kompatible Adapter Ende-zu-Ende, die parametrisierte Resilienztheorie ueber
  jeden Adapter, und die **MCP-KI-Tools**.
- **E2E** — das `AiLocalFixture` bootet die App, die auf den Fake-Server zeigt (oder einen **echten** Anbieter, wenn
  der Entwickler `AI_E2E_BASEURL` setzt (+ optional `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  echte Berechtigungen gewinnen), und steuert jede KI-Funktion durch die echte UI. Das Hinzufuegen oder Aendern einer KI-Funktion
  **erfordert** einen E2E-Test durch dieses Fixture (siehe Test-Mandat des Repos). Eine Opt-in-Spur
  (`AI_LOCAL_LLM=1`) fuehrt eine echte Vervollstaendigung durch einen **Ollama**-Testcontainer.

## Integrierte lokale KI — Null-Setup standardmaessig

Das integrierte ONNX lokale LLM funktioniert out-of-the-box: Wenn sein Modellverzeichnis fehlt und
`App:Ai:BuiltIn:AutoDownload` `true` ist (der Standard), laedt die App das Modell einmal im
Hintergrund von `App:Ai:BuiltIn:DownloadBaseUrl` herunter. Waehrend der Download laeuft, geben KI-Aufrufe (und **Test
Verbindung** in Einstellungen → KI) eine klare "Modell wird heruntergeladen (Erstinstallations-Setup)"-Meldung
zurueck anstatt eines harten Fehlers. Air-gapped/Metered-Bereitstellungen setzen `AutoDownload=false` und
stellen das Modellverzeichnis vor (`App:Ai:BuiltIn:ModelPath`). Das White-Label-
`App:Branding:AllowBuiltInAi`-Gate gilt weiterhin.

Der Download wird auch **bei Startup vorab erwaermt**, wenn das integrierte Modell der aktive Anbieter ist, damit es vor dem ersten KI-Klick bereit ist, anstatt diesen Klick mit "wird heruntergeladen…" zu sabotieren. **Einstellungen → KI** zeigt den Live-Installationsstatus auf der integrierten Anbieter-Karte – *Modell bereit* / *Modell wird heruntergeladen…* / *Modell nicht installiert* / *Download fehlgeschlagen* – mit einer Schaltflaeche **Modell herunterladen** (oder **Download erneut versuchen**), die den One-Time-Hintergrund-Abruf auf Anfrage startet (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`). Das Aktivieren des integrierten Anbieters aus Einstellungen verwendet die bereits gesaete Reihe, anstatt ein Duplikat hinzuzufuegen, damit es nie mit der Single-Active-Provider-Beschraenkung in Konflikt geraet.
