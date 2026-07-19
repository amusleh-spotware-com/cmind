---
description: "A cMind AI szolgáltató-független — Anthropic, OpenAI, Azure OpenAI, Google Gemini és bármely OpenAI-kompatibilis végpont, beleértve a lokális modelleket (Ollama, LM Studio, vLLM). Válassz szolgáltatót, modellt és végpontot; minden AI funkció változatlanul működik."
---

# AI funkciók

A cMind AI rétege **szolgáltató-független**. Minden funkció egy egységes, szolgáltató-semleges felületen keresztül kommunikál (`IAiClient.CompleteAsync`); egy **routing kliens** oldja fel az aktív szolgáltató hitelesítő adatait és küldi tovább a megfelelő wire adapternek. Te választasz szolgáltatót + modellt + végpontot (és ha a szolgáltató igényli, egy kulcsot); minden meglévő funkció változatlanul működik ugyanazzal a jogosultságkezeléssel, titkosítással, rugalmassággal és degradációval.

**Bekapcsolva, ki be:** egy **beépített lokális LLM kerül a szállításra és alapértelmezés szerint be van kapcsolva** (Microsoft.ML.OnnxRuntimeGenAI, pl. Phi-3-mini) — így minden telepítésnek működő AI van **kulcs nélkül és külső szolgáltatás nélkül**. Egy white-label telepítés eltávolíthatja, és korlátozhatja, hogy mely szolgáltatókat adhatnak hozzá a felhasználók. A beépített mellett bármely külső szolgáltatót lehet csatlakoztatni.

Támogatott szolgáltatók:

- **Beépített lokális AI** (`BuiltInOnnx`) — folyamaton belüli ONNX GenAI modell, kulcs nélkül, szállítva + alapértelmezés szerint bekapcsolva.
- **Anthropic** (Claude — Messages API)
- **OpenAI** és **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Bármely OpenAI-kompatibilis végpont**, beleértve a **lokális modelleket** (Ollama, LM Studio, vLLM, llama.cpp `server`, LocalAI) és az OpenAI-kompatibilis felhőket (**Kimi / Moonshot** az `https://api.moonshot.ai/v1/` címen, OpenRouter, Groq, Together, Mistral, DeepSeek) — mindezt egy OpenAI-kompatibilis adapteren keresztül, csak a base URL + modell + kulcs különbözteti meg őket. A Hozzáadás-szolgáltató dialog egy kattintásos **presetek** készletét kínálja (Kimi, OpenAI, OpenRouter, Groq, DeepSeek, Mistral, Ollama, LM Studio), amelyek kitöltik az alap URL-t + egy mintamodellt.

Pontosan **egy** szolgáltató aktív egyszerre. A hitelesítő adatok **titkosítva** vannak tárolva (`AiProviderCredential` aggregátum + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`); egy lokális végpontnak **nem kell kulcs**. Ha **nincs** aktív szolgáltató, minden funkció a letiltott eredményt adja vissza, és az alkalmazás többi része változatlanul fut (kulcs nem szükséges az platform felépítéséhez, teszteléséhez vagy futtatásához).

**Visszafelé kompatibilis:** egy meglévő telepítés örökölt `App:Ai:ApiKey` (vagy a régi titkosított `ai.api_key` beállítás) automatikusan az alapértelmezett aktív **Anthropic** szolgáltatóként kerül tiszteletbe — nulla beavatkozás szükséges.

Ha az AI nincs konfigurálva → az AI oldalak elhalványítják a műveleteket és megjelenítenek egy bannert plusz egy egyszeri felszólítást, hogy adj hozzá egy szolgáltatót **Beállítások → AI** (`AiFeatureNotice`). Állapot: `GET /api/ai/status` (`{ enabled, kind, model }`); szolgáltatók kezelése (csak tulajdonos) `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`, `DELETE /api/ai/providers/{id}`, és egy `POST /api/ai/providers/test` kapcsolódási ping.

## Telepítési alapértelmezés vs. a felhasználó saját szolgáltatója

Az AI hitelesítő adatoknak két hatóköre van:

- **Telepítési alapértelmezés (tulajdonos által kezelt).** A tulajdonos konfigurál egy szolgáltatót (vagy szállít egyet az `App:Ai:Providers[]` / az örökölt `App:Ai:ApiKey` révén). Az minden felhasználó **megosztott alapértelmezése** lesz — így egy bróker vagy hosting szolgáltató finanszírozhatja az AI-t minden felhasználójának **teljesítmény-nincs per-felhasználó beállítás és nincs per-felhasználó korlát** mellett. A fenti tulajdonos-only `/api/ai/providers` útvonalakon keresztül kezelhető.
- **A felhasználó saját szolgáltatója (önkiszolgáló).** Bármely bejelentkezett felhasználó hozzáadhatja a saját szolgáltatóját `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`, `DELETE /api/ai/my-providers/{id}` útvonalakon. Ha van ilyen, a **saját aktív szolgáltató felülírja a telepítési alapértelmezést** a saját AI funkcióihoz; eltávolítása visszaáll az alapértelmezésre.

**Feloldási sorrend** (`AiProviderStore`, kérésenkénti felhasználó): a felhasználó saját aktív hitelesítő adata → a telepítési alapértelmezés → az örökölt konfigurációs kulcs → nincs (AI letiltva). Pontosan egy hitelesítő adat aktív **hatókörönként** (részleges egyedi index `OwnerUserId` szerint), és minden hatókör egymástól függetlenül oldódik fel, így a felhasználó saját kulcsának aktiválása soha nem zavarja meg a megosztott alapértelmezést. Háttér/nem-Web kontextusok (nincs kérés-felhasználó) mindig a telepítési alapértelmezést oldják fel.

## Szolgáltató képességmátrix

A képességek alapértelmezés szerint szolgáltatónként eltérőek és a tulajdonos felülbírálhatja. Ha egy képesség ki van kapcsolva, a funkció ** degradálódik, soha nem dob kivételt**: a webes keresés csendben elmarad; a vision típusú képtelenség hibaüzenetet ad.

| Szolgáltató | Fajta | Alapértelmezett base URL | Kulcs szükséges | Web keresés | Vision | Megjegyzések |
|---|---|---|---|---|---|---|
| Beépített lokális AI | `BuiltInOnnx` | n/a (folyamaton belül) | nem | ✖ | ✖ | szállított ONNX GenAI modell, alapértelmezés szerint be |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | igen | ✅ | ✅ | Messages API, `web_search` eszköz |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | igen | opcionális | opcionális | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | igen | ✅ | ✅ | deployment útvonal + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | igen | ✅ | ✅ | `generateContent`, `google_search` grounding |
| Ollama (lokális) | `OpenAiCompatible` | `http://localhost:11434/v1/` | nem | ✖ | modell-függő | OpenAI-kompatibilis adapteren át |
| LM Studio (lokális) | `OpenAiCompatible` | `http://localhost:1234/v1/` | nem | modell-függő | modell-függő | OpenAI-kompatibilis adapteren át |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | a kiszolgált URL | nem | ✖ | modell-függő | OpenAI-kompatibilis adapteren át |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | szolgáltató URL | igen | ✖ | modell-függő | OpenAI-kompatibilis adapteren át |

A teljes szolgáltató-specifikus beállítási útmutatók (kulcsok, URL-ek, modell azonosítók, UI lépések): lásd [AI szolgáltatók — beállítási katalógus](../deployment/ai-providers.md).

## Beépített lokális AI (szállítva, alapértelmezés szerint be)

A cMind egy **valódi lokális LLM-et szállít, amely folyamaton belül fut** a [Microsoft.ML.OnnxRuntimeGenAi](https://onnxruntime.ai/docs/genai/) révén (egy kompakt instruct modell, például Phi-3-mini). Nem kell **API kulcs és külső szolgáltatás**, és az első indításkor — amikor nincs szolgáltató konfigurálva és a white-label gate engedi — **automatikusan seedelve és aktiválva van**, így minden telepítésnek működő AI van a dobozból kivéve.

- A modell könyvtár (`genai_config.json` + tokenizer + súlyok) az `App:Ai:BuiltIn:ModelPath` által konfigurált (alapértelmezés: `models/onnx`, az alkalmazás alapkönyvtárához viszonyítva). Amikor a modell fájlok hiányoznak, a szolgáltató **degradál egy típusolt hibaüzenetre egy telepítési tippel** — soha nem dob kivételt, és az alkalmazás többi része érintetlen marad.
- Minden szöveges AI funkciót működtet. Mivel kompakt modell, csak szöveges (nincs szerveroldali web keresés vagy vision) és a generálás serializált (egy modell példány, újrafelhasználva lazy load után).
- **Több beépített modell is egyszerre létezhet.** Minden letöltött modell az `ModelPath/<key>` alatt élhet; egy kurátált katalógus (Phi-3.5-mini alapértelmezett, valamint Phi-3-mini-128k) letölthető és választható a **Beállítások → AI** menüből. Egy beépített almodell kiválasztása in-process-ben betölti azt. Modell beszerzése/csomagolása: lásd [AI szolgáltatók → beépített](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## White-label vezérlők

Egy white-label telepítés korlátozza az AI-t az `App:Branding` révén (szerveroldalon kényszerítve minden szolgáltató upsert-en):

- `AllowBuiltInAi` (alapértelmezés `true`) — állítsd `false`-ra a **beépített modell teljes eltávolításához**.
- `AllowLocalProviders` (alapértelmezés `true`) — állítsd `false`-ra a lokális/saját-gazda végpontok tiltásához (loopback / privát OpenAI-kompatibilis, pl. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (alapértelmezés üres = mind) — csak azokat a fajtákat listázd, amelyeket a telepítés engedélyez (pl. `["Anthropic","OpenAiCompatible"]`), hogy zárold, mely szolgáltatókat adhatnak hozzá a felhasználók.
- `AllowAiModelManagement` (alapértelmezés `true`) — állítsd `false`-ra a **modell böngészés**, a **funkciónkénti modellválasztó**, és a **funkciónkénti model kötés** elrejtéséhez. Mindegyik a tulajdonos által futásidőben hangolható a **Beállítások → Telepítés** menüből (élőben átfedett az `IOptionsMonitor`-on) és katalógusban van az `WhiteLabelCatalog`-ban.

## Bővítés: jövőbeli beépített modellek

Az AI réteg **adapter-alapú és bővíthető**. Minden szolgáltató egy `IAiProvider`, amelyet `AiProviderKind` alapján választanak ki; a funkció-felülete (`IAiClient`/`AiFeatureService`) soha nem változik. Egy új beépített modell futásidejének hozzáadása később (egy másik ONNX modell, egy másik folyamaton belüli motor, GGUF/llama.cpp in-proc stb.) lokalizált változtatás: adj hozzá egy `AiProviderKind`-t, implementálj egy `IAiProvider` adaptert, regisztráld, és (opcionálisan) kötözdd az alapértelmezett seedelést + egy dialog opciót — nincs funkció, végpont vagy MCP eszköz változtatás. A beépített ONNX szolgáltató ennek a minta a referencia implementációja.

## Képességek

- **cBot építése** — egyszerű angol prompt → futtatható cBot via **generál → épít → AI-javítás** ön-javító hurok (`build-strategy`), `/ai/build` címen. A **generált forráskód megjelenik** amikor az épület befejeződik (másolás gombbal), az építési naplóval együtt (szintén másolható) — siker **és** kudarc esetén is. Még egy sikertelen build is mentésre kerül a cBots-aidban (a tényleges egyedi névvel) és egy *Megnyitás szerkesztőben* linket kínál a fordítási hibák javításához és az újraépítéshez, ahelyett hogy elveszítenéd a munkát.
- **Funkciónkénti modellválasztó** — minden AI funkció oldal és dialog megjeleníti a **modellválasztót**, amely felsorolja az elérhető modelleket (saját szolgáltatók + telepítési alapértelmezések). Az oldal mentett kötése van előválasztva, ha van, egyébként az **alapértelmezett** modell, és a kiválasztott modell az adott műveletre vonatkozik (elküldve `?modelId=` paraméterként, és a `RoutingAiClient` által kényszerítve az adott híváshoz). Rejtett, ha a telepítés letiltja a modellkezelést.
- **Modellek böngészése és kiválasztása funkciónként** — böngésszen azokat a modelleket, amelyeket egy szolgáltató végpontja meghirdet (`GET /v1/models` az LM Studio / Ollama / vLLM / llama.cpp között, vagy a beépített katalógus), ahelyett hogy kézzel beírna egy id-t, és **kösse az egyes AI funkciókat egy másik modellhez** így több modell szolgál egyszerre más-más funkciókat (egy kötetlen funkció visszaáll a hatókör alapértelmezett szolgáltatójára).
- **Paraméter optimalizálás** — zárt hurok: AI javasol paraméterkészleteket, mindegyik perzisztálva + backtesztelve a node-okon (`optimize-run` / `optimize-params`).
- **Autonóm portfólió ügynök** — mandátum-vezérelt javaslatok teljes döntési naplóval (`AgentMandate` → `AgentProposal`).
- **Kockázati őr működésben** — `AiRiskGuard` háttérszolgáltatás értékeli a futó botokat, képes **automatikusan leállítani** kritikus kockázat esetén (opcionális).
- **Prop-firm kitettségi őr** — drawdown/kitevtségi limitek automatikus flatten-nel.
- **Piaci riasztások** — `AlertRule` motor AI szentimentummal (web keresés grounded ahol a szolgáltató támogatja).
- **Elemzés** — cBot értékelés, backtest elemzés, post-mortem-ek, piaci szentimentum, chart-vision tervezés, marketplace kurálás.

## Felületek

- Web végpontok `/api/ai/*` alatt (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …). Minden funkció végpont elfogad egy opcionális `?modelId=<credential>` paramétert az adott hívás futtatásához egy kiválasztott modellen. Plusz **modell felfedezés** (`/api/ai/models/probe`, `/api/ai/usable-models`) és **funkciónkénti kötések** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- MCP eszközök (`AiTools`) AI kliensek számára — lásd [mcp.md](mcp.md). A szolgáltató kiválasztás átlátszó az MCP kliensek számára.
- **AI** nav csoport — egy Blazor **oldal funkciónként**: cBot építése (`/ai/build`), Értékelés (`/ai/review`), Vita (`/ai/debate`), Piaci Szentimentum (`/ai/sentiment`), Kitettség ellenőrzés (`/ai/exposure`), Portfólió Áttekintés (`/ai/digest`), Hangolási Tanácsadó (`/ai/tune`), Optimalizálás (`/ai/optimize`), valamint Portfólió Ügynök, Riasztások, MCP Kulcsok. Az oldalak megosztják az `AiFeaturePageBase` + `AiOutputPanel` + egy `AiModelSelect`-et; mindegyik megmutatja az `AiFeatureNotice`-t, ha nincs szolgáltató konfigurálva.
- **Beállítások → AI** (`/settings/ai`, csak tulajdonos) — szolgáltató lista egy **Hozzáadás / szerkesztés szolgáltató dialog-kal** (fajta, base URL fajta-specifikus tippekkel, egy kattintásos presetek, beleértve a **Kimi/Moonshot**, Ollama és LM Studio kínálatot, modell, opcionális kulcs, képesség togglek, "állítsd aktívra") és egy **Tesztelés kapcsolat** gombbal.

## Konfiguráció

Az `App:Ai` támogatja mind az örökölt egykulcsos, mind a multi-szolgáltató seedelést:

- Örökölt: `ApiKey`, `Model` (alapértelmezés `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — továbbra is tiszteletben tartva, mint az alapértelmezett Anthropic szolgáltató.
- Multi-szolgáltató: `ActiveProvider` (fajta) és `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?, MaxTokens?, Capabilities? }`) — importálva az áruházba indításkor, ha még nincs hitelesítő adat, így egy ops csapat konfigurált (beleértve a lokális-LLM-et) telepítést szállíthat kizárólag appsettings/env révén.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` változatlan. Tesztek/dev számára egy konfigurációs kulcs él az egységes [dev-credentials fájlban](../testing/dev-credentials.md) az `Ai` alatt.

## Megbízhatóság

A szolgáltató megbízhatatlanként van kezelve — semmi, amit csinál, nem tudja lehozni az alkalmazást. Ez azonosan igaz a felhős és lokális végpontokra (egy halott Ollama újrapróbál, majd degradál pontosan úgy, mint egy throttlet Anthropic):

- **Graceful degradáció.** Minden hiba mód (nincs szolgáltató, HTTP 4xx/5xx/429, timeout, rossz test, üres tartalom, nem támogatott képesség) egy típusolt `AiResult.Fail(reason)`-t ad vissza — a kliens soha nem dob kivételt egy oldalra, MCP eszközre vagy hosted szolgáltatásra.
- **Rugalmassági pipeline.** `AddAiHttpClient` ad az egy megosztott AI `HttpClient`-nek korlátozott újrapróbálást átmeneti 5xx / hálózati hibákra (exponenciális backoff + jitter) plusz generózus per-attempt és összes időtúllépések (`AiHttp`), újrafelhasználva minden adapter által.

## Tesztelés a fake lokális LLM-mel

Az AI réteg **külső függőség nélkül bizonyított** end-to-end a `FakeLocalLlmServer` révén — egy apró folyamaton belüli **OpenAI-kompatibilis** végpont, determinisztikus konzerv választ visszaadva, wire-azonos Ollama/LM Studio/vLLM-hez. Ez alátámasztja:

- **Egységteszt** — adapterenkénti request-transzláció + response-parse tesztek, routing/képesség degradáció.
- **Integráció** — az OpenAI-kompatibilis adapter end-to-end, a parametrizált rugalmassági elmélet minden adapteren, és az **MCP AI eszközök**.
- **E2E** — az `AiLocalFixture` bootolja az alkalmazást a fake szerverre mutatva (vagy egy **valódi** szolgáltatóra, ha a fejlesztő beállítja az `AI_E2E_BASEURL`-et (+ opcionális `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) — valódi creds nyer) és minden AI funkciót a valódi UI-n keresztül hajt. Bármely AI funkció hozzáadása vagy változtatása **megkövetel** egy E2E tesztet ezen fixtureen keresztül (lásd a repo teszt mandátum). Egy opcionális sáv (`AI_LOCAL_LLM=1`) egy valódi completiont futtat egy **Ollama** Testcontainer-en.

## Beépített lokális AI — nulla-beállítás alapértelmezés szerint

A beépített ONNX lokális LLM dobozból működik: amikor a modell könyvtára hiányzik és `App:Ai:BuiltIn:AutoDownload` `true` (az alapértelmezés), az alkalmazás egyszer letölti a modellt a háttérben `App:Ai:BuiltIn:DownloadBaseUrl`-ről. Amíg a letöltés fut, az AI hívások (és a **Kapcsolat tesztelése** a Beállítások → AI-ban) egy egyértelmű "a modell letöltődik (első alkalommal setup)" üzenetet adnak vissza kemény hiba helyett. Air-gapped/metered telepítések beállítják az `AutoDownload=false`-t és előre biztosítják a modell könyvtárat (`App:Ai:BuiltIn:ModelPath`). A white-label `App:Branding:AllowBuiltInAi` gate továbbra is alkalmazandó.

A letöltés **előre fel van melegítve az indításkor** amikor a beépített modell az aktív szolgáltató, így az első AI kattintás előtt kész, nem pedig azzal kudarc "letöltödik…". **Beállítások → AI** megjeleníti az élő telepítési állapotot a beépített szolgáltató kártyáján — *Modell kész* / *Modell letöltödik…* / *Modell nincs telepítve* / *Letöltés sikertelen* — egy **Modell letöltése** (vagy **Próba újra letöltés**) gombbal, amely az egy alkalommal háttér-fetch-et indítja igény szerint (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`). A beépített szolgáltató engedélyezése a Beállítások-ból újrafelhasználja az már seedelve sor helyett egy duplikátum hozzáadása, így soha nem ütközik az egyetlen-aktív-szolgáltató korláttal.
