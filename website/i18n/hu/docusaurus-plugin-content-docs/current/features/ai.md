---
description: "A cMind AI szolgáltató-agnosztikus — Anthropic, OpenAI, Azure OpenAI, Google Gemini, és bármilyen OpenAI-kompatibilis végpont, beleértve a helyi modelleket (Ollama, LM Studio, vLLM). Válasszon szolgáltatót, modellt és végpontot; minden AI funkció megváltozatlan működésű."
---

# AI funkciók

A cMind AI rétege **szolgáltató-agnosztikus**. Minden funkció egy egyetlen szolgáltató-semleges varrattal beszél (`IAiClient.CompleteAsync`); egy **útválasztó kliens** feloldja az aktív szolgáltató hitelesítési adatokat és elküldi a megfelelő vezeték adapterhez. Válassz egy szolgáltatót + modellt + végpontot (és ha a szolgáltató igényli, egy kulcsot); minden meglévő funkció megváltozatlan működésű azonos kapuzással, titkosítással, rezilienciával és degradációval.

**Akkumulátor beépített:** egy **beépített helyi LLM szállítva az alkalmazással és alapértelmezés szerint engedélyezett** (Microsoft.ML.OnnxRuntimeGenAI, pl. Phi-3-mini) — így minden üzembe helyezésnek működő AI van **API kulcs és külső szolgáltatás nélkül**. Egy fehér címkés telepítés eltávolíthatja és korlátozhatja, mely szolgáltatókat adhatják hozzá a felhasználók. A beépülőn túl bármilyen külső szolgáltatóhoz csatlakozz.

Támogatott szolgáltatók:

- **Beépített helyi AI** (`BuiltInOnnx`) — in-process ONNX GenAI modell, nincs kulcs, szállított + alapértelmezés bekapcsolt.
- **Anthropic** (Claude — Messages API)
- **OpenAI** és **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Bármilyen OpenAI-kompatibilis végpont**, beleértve **helyi modelleket** (Ollama, LM Studio, vLLM, llama.cpp `server`, LocalAI) és OpenAI-kompatibilis felhőket (OpenRouter, Groq, Together, Mistral, DeepSeek) — mind az egy OpenAI-kompatibilis adapter segítségével, csak az alapvető URL + modell + kulcs alapján eltérően.

Pontosan **egy** szolgáltató aktív egyszerre. A hitelesítési adatok **titkosítottan** tároltak (`AiProviderCredential` aggregátum + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`); egy helyi végpont **nincs szükség kulcsra**. **Nincs** aktív szolgáltató esetén minden funkció a letiltott eredményt adja vissza és az alkalmazás többi része megváltozatlan marad (nincs szükség kulcsra az alkalmazás felépítéséhez, teszteléséhez vagy futtatásához).

**Visszafelé kompatibilitás:** egy meglévő telepítés örökölt `App:Ai:ApiKey` (vagy a régi titkosított `ai.api_key` beállítás) automatikusan tiszteletben tartódik az alapértelmezett aktív **Anthropic** szolgáltatóként — nulla cselekvés szükséges.

AI konfigurálva nincs → az AI oldal halványít műveletek és megjelenít egy szalagot valamint egy egyszeri kérést egy szolgáltató hozzáadásához az **Beállítások → AI** ('AiFeatureNotice')-ben. Állapot a `GET /api/ai/status` értéknél (`{ enabled, kind, model }`); szolgáltatók kezelve (csak a tulajdonos számára) `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`, `DELETE /api/ai/providers/{id}`, és `POST /api/ai/providers/test` csatlakozási pingeléshez.

## Telepítési alapértelmezés vs. egy felhasználó saját szolgáltatója

Az AI hitelesítési adatok két hatókörrel rendelkeznek:

- **Telepítési alapértelmezés (tulajdonoskezelés)**. A tulajdonos konfigurál egy szolgáltatót (vagy szállít egyet az `App:Ai:Providers[]` / az örökölt `App:Ai:ApiKey` segítségével). Ez lesz az **összes felhasználó megosztott alapértelmezése** — így egy bróker vagy üzemeltetési szolgáltató finanszírozhat AI-t az összes felhasználójának **felhasználónkénti beállítás és felhasználónkénti korlát nélkül**. Kezelve a fent említett csak a tulajdonos `/api/ai/providers` útvonalak segítségével.
- **Egy felhasználó saját szolgáltatója (önkiszolgáló)**. Bármilyen bejelentkezve tartózkodó felhasználó hozzáadhat saját szolgáltatót a `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`, `DELETE /api/ai/my-providers/{id}` alatt. Ha jelen van, azok **saját aktív szolgáltatója felülbírálódik a telepítési alapérték alapján a saját AI funkcióikhoz**; eltávolítás visszaesik az alapérték-re.

**Feloldás sorrendje** (az `AiProviderStore`-ban, kérelem felhasználó alapján): a felhasználó saját aktív hitelesítési adatai → a telepítési alapértelmezés → a örökölt konfiguráció kulcs → nincs (AI letiltva). Pontosan egy hitelesítés aktív **hatókör alapján** (részleges egyedi index `OwnerUserId` alapján), és minden hatókör egymástól függetlenül feloldódik, így egy felhasználó aktiválva saját kulcsát soha nem zavarja a megosztott alapérték. Background/nem-Web kontextus (nem kérelem felhasználó) mindig a telepítési alapérték feloldódik.

## Szolgáltató képesség mátrix

A képességek alapértelmezés szolgáltató alapján és a tulajdonos felülbíráló. Amikor egy képesség ki van kapcsolva a funkció **romlódik, soha nem dobnak hiba**: web keresés csendes ledobódik; a látásmód egy gépelt képesség-nem-támogatott kudarc adja vissza.

| Szolgáltató | Fajta | Alapértelmezés alapvető URL | Kulcs szükséges | Web keresés | Látásmód | Megjegyzések |
|---|---|---|---|---|---|---|
| Beépített helyi AI | `BuiltInOnnx` | n/a (in-process) | nem | ✖ | ✖ | szállított ONNX GenAI modell, alapértelmezés bekapcsolt |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | igen | ✅ | ✅ | Messages API, `web_search` eszköz |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | igen | opcionális | opcionális | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | igen | ✅ | ✅ | telepítési útvonal + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | igen | ✅ | ✅ | `generateContent`, `google_search` alap |
| Ollama (helyi) | `OpenAiCompatible` | `http://localhost:11434/v1/` | nem | ✖ | modell-függő | OpenAI-kompatibilis adapter segítségével |
| LM Studio (helyi) | `OpenAiCompatible` | `http://localhost:1234/v1/` | nem | modell-függő | modell-függő | OpenAI-kompatibilis adapter segítségével |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | te szolgáltatott URL | nem | ✖ | modell-függő | OpenAI-kompatibilis adapter segítségével |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | szolgáltató URL | igen | ✖ | modell-függő | OpenAI-kompatibilis adapter segítségével |

Teljes szolgáltatónkénti beállítási útmutatók (kulcsok, URL-ek, modell azonosítók, UI lépések): lásd [AI szolgáltatók — beállítási katalógus](../deployment/ai-providers.md).

## Beépített helyi AI (szállított, alapértelmezés bekapcsolt)

A cMind szállít egy **valódi helyi LLM-et, amely in-process fut** az [Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) segítségével (egy kompakt utasítás modell, például Phi-3-mini). **Nincs szükség API kulcsra és nincs szükség külső szolgáltatásra**, és az első indítás — amikor nincs konfigurálva szolgáltató és a fehér címke kapu engedélyezi — **vetett és aktiválódik automatikusan**, így minden telepítésnek működő AI van dobozból kívül.

- A modell könyvtár (`genai_config.json` + tokenizer + súlyok) az `App:Ai:BuiltIn:ModelPath` által konfigurálva van (alapértelmezés `models/onnx`, az alkalmazás alapkönyvtárához relatív). Amikor a modell fájlok hiányzanak a szolgáltató **romlódik egy gépelt kudarc telepítési útmutatóval** — soha nem dob hiba, és az alkalmazás többi része nincs hatása.
- Ez működteti az összes szöveges AI funkciót. Mivel egy kompakt modell, ez csak szöveges (nincs szerver oldali web keresés vagy látásmód) és a generálás szoftveres (egy modell példány, újrahasznosított egy lusta terhelés után).
- Szerzés/csomag a modell: lásd [AI szolgáltatók → beépített](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## Fehér címke vezérlők

Egy fehér címke telepítés korlátozza az AI-t az `App:Branding` segítségével (kiszolgáló oldali erőltetett minden szolgáltató felső-írása):

- `AllowBuiltInAi` (alapértelmezés `true`) — állítsd `false`-ra az **beépített modell teljes eltávolításához**.
- `AllowLocalProviders` (alapértelmezés `true`) — állítsd `false`-ra a helyi/önmagukat üzemeltetett végpontok tiltásához (loopback / privát OpenAI-kompatibilis, pl. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (alapértelmezés üres = összes) — lista csak a fajták, amelyek az telepítés jóváhagyott (pl. `["Anthropic","OpenAiCompatible"]`) a lezárkózáshoz mely szolgáltatókat adhatnak hozzá a felhasználók.

## Kiterjesztés: jövő beépített modellek

Az AI réteg **adapter alapú és beépített növekedési készültség**. Minden szolgáltató egy `IAiProvider` az `AiProviderKind` választódik; a funkció felé fordulva varrat (`IAiClient`/`AiFeatureService`) soha nem változik. Új beépített modell futási idő hozzáadása később (egy másik ONNX modell, egy másik in-process motor, GGUF/llama.cpp in-proc, stb.) egy lokalizált csere: add egy `AiProviderKind`, implementálni egy `IAiProvider` adapter, regisztrálni, és (opcionálisan) vezet alapértelmezés vetés + egy dialógus opció — nincs funkció, végpont, vagy MCP eszköz csere. A beépített ONNX szolgáltató ez mintaer referencia-implementáció.

## Képességek

- **Build cBot** — egyszerű angol kérés → futtatható cBot az **generálás → felépítés → AI-javítás** öngyógyító hurok segítségével (`build-strategy`), az `/ai/build`-nél.
- **Paraméter optimalizálás** — zárt hurok: AI javasolja a paraméter készleteket, mindegyik kitartó + backtest a csomópontok között (`optimize-run` / `optimize-params`).
- **Autonóm portfólió ügynök** — mandátum hajtott javaslatok teljes döntési naplóval (`AgentMandate` → `AgentProposal`).
- **Előadó kockázati közvetítő** — `AiRiskGuard` háttérszolgáltatás értékeli a futó botokat, képes **auto-megállítás** kritikus kockázaton (opcionális).
- **Prop-firm expozíció őr** — leszálló/expozíció korlátok auto-kilapítással.
- **Piaci riasztások** — `AlertRule` motor AI érzékeléssel (web-keresés alapozva ahol a szolgáltató támogatja).
- **Elemzés** — cBot felülvizsgálat, backtest elemzés, utólagos boncolás, piaci érzékelés, diagram-látásmód tervezés, piactér kurálása.

## Felszínek

- Web végpontok az `/api/ai/*` alatt (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …).
- MCP eszközök (`AiTools`) az AI kliensekhez — lásd [mcp.md](mcp.md). A szolgáltató kiválasztása átlátszó az MCP kliens számára.
- **AI** nav csoport — egy Blazor **lap funkcióként**: Build cBot (`/ai/build`), Felülvizsgálat (`/ai/review`), Vita (`/ai/debate`), Piaci érzékelés (`/ai/sentiment`), Expozíció ellenőrzés (`/ai/exposure`), Portfólió kivonat (`/ai/digest`), Hangol tanácsadó (`/ai/tune`), Optimalizálás (`/ai/optimize`), plusz Portfólió ügynök, Riasztások, MCP kulcsok. Lapok osztják az `AiFeaturePageBase` + `AiOutputPanel`; mindegyik megjeleníti az `AiFeatureNotice` amikor nincs konfigurálva szolgáltató.
- **Beállítások → AI** (`/settings/ai`, csak tulajdonos) — szolgáltató lista egy **Hozzáadás / szerkesztés szolgáltató dialógussal** (fajta, alapvető URL szolgáltatónkénti útmutatókkal incl. egy Ollama/LM Studio localhost előbeállítás, modell, opcionális kulcs, képesség váltók, "aktívnak beállít") és egy **Test csatlakozási** gombbal.

## Konfiguráció

Az `App:Ai` támogatja az örökölt egyetlen kulcsot és több-szolgáltató vetést:

- Örökölt: `ApiKey`, `Model` (alapértelmezés `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — még mindig tiszteletben tartva az alapértelmezett Anthropic szolgáltatóként.
- Több-szolgáltató: `ActiveProvider` (fajta) és `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?, MaxTokens?, Capabilities? }`) — importálva a boltba indításon ha nincs hitelesítés még, így egy ops csapat szállíthat egy konfigurált (incl. helyi-LLM) telepítést tisztán az appsettings/env segítségével.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` megváltozatlan. A tesztek/dev, egy config kulcs él az egységes [dev-hitelesítési fájl](../testing/dev-credentials.md) alatt az `Ai` alatt.

## Megbízhatóság

A szolgáltató megbízhatatlannak kezelkedik — semmi amit csinál nem tudja levenni az alkalmazást. Ez azonos módon köztörvényadódik felhő és helyi végpontokra (egy halott Ollama újrapróbálja majd romlódik pontosan mint egy szabályozott Anthropic):

- **Kecses degradálódás.** Minden kudarc üzemmód (nincs szolgáltató, HTTP 4xx/5xx/429, időtúllépés, helytelenül formázott test, üres tartalom, nem támogatott képesség) gépelt `AiResult.Fail(reason)`-t adja vissza — a kliens soha nem dob lapba, MCP eszközbe, vagy üzemeltetett szolgáltatásba.
- **Reziliencia csővezeték.** Az `AddAiHttpClient` adja meg az egyetlen megosztott AI `HttpClient`-t egy korlátolt újrapróbálkozást a tranziens 5xx / hálózati kudarcokon (exponenciális visszalépés + keveredés) plusz bőséges per-kísérlet és teljes időtúllépések (`AiHttp`), újrahasznosított minden adapter segítségével.

## Tesztelés az álhitelű helyi LLM-mel

Az AI réteg végtelenül bizonyított **külső függőség nélkül** az `FakeLocalLlmServer` segítségével — egy kis in-process **OpenAI-kompatibilis** végpont gépelt sarkalatos válaszadó, vezeték-azonos az Ollama/LM Studio/vLLM-mel. Ez támogatja:

- **Egység** — szolgáltatónkénti kérelem-fordítás + válasz-elemzés tesztek, útválasztás/képesség romlódás.
- **Integráció** — az OpenAI-kompatibilis adapter végtelenül, a parametrált reziliencia elmélet minden adapter között, és az **MCP AI eszközök**.
- **E2E** — az `AiLocalFixture` felindítja az alkalmazást az álkiszolgálóra mutatva (vagy egy **valódi** szolgáltató amikor a fejlesztő beállítódik `AI_E2E_BASEURL` (+ opcionális `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) — valódi hitelesítés nyer) és meghajtja az összes AI funkciót a valódi UI-n. Új AI funkció hozzáadása vagy módosítása **igényli** az E2E tesztet ezen keresztül ezt az állványt (lásd a repo teszt megbízatás). Egy opcionális módba (`AI_LOCAL_LLM=1`) futtat egy valódi befejezés egy **Ollama** Testcontainer segítségével.
