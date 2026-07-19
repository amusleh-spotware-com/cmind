---
description: "cMind AI je agnostičen do ponudnika — Anthropic, OpenAI, Azure OpenAI, Google Gemini in kateri koli OpenAI-kompatibilen končni točki, vključno z lokalnimi modeli (Ollama, LM Studio, vLLM). Izberite ponudnika, model in končno točko; vsaka značilnost AI deluje nespremenjena."
---

# Značilnosti AI

cMind-ova raven AI je **agnostična do ponudnika**. Vsaka značilnost govori s samo eno pantomimo,
neodvisno od ponudnika (`IAiClient.CompleteAsync`); **usmerjevalni odjemalec** razreši aktivno
pooblastilo ponudnika in ga pošlje ujemajočem adapterju žice. Izberete ponudnika + model + končno
točko (in, če jo ponudnik potrebuje, ključ); vsaka obstoječa značilnost deluje nespremenjena z
istim vratarjenjem, šifriranjem, premoženjem in degradacijo.

**Baterije vključene:** **vgrajeni lokalni LLM se pošilja z aplikacijo in je privzeto omogočen**
(Microsoft.ML.OnnxRuntimeGenAI, npr. Phi-3-mini) — torej vsaka implementacija ima delujočo AI **brez
ključa API in brez eksterne storitve**. Implementacija belo oznake lahko odstrani in omeji, katere
ponudnike smejo uporabniki dodati. Poleg vgrajene, povežite kateri koli zunanji ponudnik.

Podprti ponudniki:

- **Vgrajeni lokalni AI** (`BuiltInOnnx`) — model ONNX GenAI v postopku, brez ključa, odpremljen
  + privzeto.
- **Anthropic** (Claude — Messages API)
- **OpenAI** in **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Kateri koli OpenAI-kompatibilen končni točki**, vključno z **lokalnimi modeli** (Ollama, LM Studio, vLLM, llama.cpp `server`, LocalAI) in OpenAI-kompatibilnimi oblaki (**Kimi / Moonshot** na `https://api.moonshot.ai/v1/`, OpenRouter, Groq, Together, Mistral, DeepSeek) — vse prek enega OpenAI-kompatibilnega adapterja, se razlikuje le po osnovni URL + model + ključ. Dialog za dodajanje ponudnika nudi **prednastavke na eno klic** (Kimi, OpenAI, OpenRouter, Groq, DeepSeek, Mistral, Ollama, LM Studio), ki izpolnijo osnovno URL in vzorčni model.

Natančno **en** ponudnik je aktiven naenkrat. Poverenja so shranjena **šifrirana** (`AiProviderCredential`
agregat + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`); lokalna končna
točka ne potrebuje **ključa**. Brez **aktivnega** ponudnika vsaka značilnost vrne onemogočen rezultat
in se ostala aplikacija tečika nespremenjena (ni ključa potrebnega za gradnjo, testiranje ali tečenje
platforme).

**Nazaj-skladnost:** obstoječe implementacije starešine `App:Ai:ApiKey` (ali starega šifriranega
`ai.api_key` nastavke) je čaščena samodejno kot privzeti aktivni **Anthropic** ponudnik — ni
potrebnega dejanja.

AI neurejen → stranice AI utišajo dejanja in prikažejo pasico plus enkratni poziv za dodajanje
ponudnika v **Nastavitve → AI** (`AiFeatureNotice`). Status na `GET /api/ai/status` (`{ enabled, kind,
model }`); ponudniki upravljani (samo lastnik) prek `GET/PUT /api/ai/providers`, `POST
/api/ai/providers/{id}/activate`, `DELETE /api/ai/providers/{id}` in `POST /api/ai/providers/test`
ping povezanosti.

## Privzeto implementacije vs. ponudnik lastnega uporabnika

Pooblastila AI imajo dva obsega:

- **Privzeto implementacije (upravlja lastnik).** Lastnik nastavi ponudnika (ali ga pošlje prek
  `App:Ai:Providers[]` / starega `App:Ai:ApiKey`). Postane **skupni privzeti za vsakega uporabnika** —
  torej je posrednik ali ponudnik gostovanja lahko financira AI za vse svoje uporabnike z **brez
  nastavitve na uporabnika in brez omejitve na uporabnika**. Upravljan prek samo-lastniškoih
  `/api/ai/providers` poti zgoraj.
- **Ponudnik lastnega uporabnika (samopostrežni).** Vsak vpisani uporabnik smeh dodati svojega
  ponudnika pod `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Če je prisoten, njihov **lastni aktivni ponudnik preglasa
  privzeto implementacijo za njihove lastne značilnosti AI**; odstranitev se vrne na privzeto.

**Vrstni red razrešitve** (v `AiProviderStore`, na uporabnika na zahtevo): lastno pooblastilo uporabnika →
privzetaImplementacija → starega ključ konfiguracije → nobena (AI onemogočen). Natančno eno pooblastilo
je aktivno **na obseg** (delni edinstven indeks na `OwnerUserId`), in vsak obseg se razreši neodvisno,
zato uporabnik, ki aktivira svoj ključ, nikoli ne moti skupnega privzetega. Ozadja/ne-spletnih konteksti
(ni zahtevka uporabnika) vedno razrešijo privzeto implementacijo.

## Matrika zmožnosti ponudnika

Zmožnosti privzeto na ponudniku in so preglasljive lastnika. Ko je zmožnost izklopljena značilnost
**degradira, nikoli ne vrže**: spletna iskanja je tiho padla; vid vrne nepodprto napako zmožnosti.

| Ponudnik | Vrsta | Privzeta osnovna URL | Ključ potreben | Spletna iskanja | Vid | Opombe |
|---|---|---|---|---|---|---|
| Vgrajeni lokalni AI | `BuiltInOnnx` | n/a (v postopku) | ne | ✖ | ✖ | odpremljeni model ONNX GenAI, privzeto |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | da | ✅ | ✅ | Messages API, `web_search` orodje |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | da | naključje | naključje | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | da | ✅ | ✅ | poti vzpostavitve + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | da | ✅ | ✅ | `generateContent`, `google_search` zasidranje |
| Ollama (lokalni) | `OpenAiCompatible` | `http://localhost:11434/v1/` | ne | ✖ | odvisno od modela | prek OpenAI-kompatibilnega adapterja |
| LM Studio (lokalni) | `OpenAiCompatible` | `http://localhost:1234/v1/` | ne | odvisno od modela | odvisno od modela | prek OpenAI-kompatibilnega adapterja |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | vaša URL | ne | ✖ | odvisno od modela | prek OpenAI-kompatibilnega adapterja |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | URL ponudnika | da | ✖ | odvisno od modela | prek OpenAI-kompatibilnega adapterja |

Polnih na ponudnika nastavitvenih vodičev (ključi, URL, ID modelov, koraki UI): poglejte
[Ponudniki AI — katalog nastavitev](../deployment/ai-providers.md).

## Vgrajeni lokalni AI (odpremljeni, privzeto-prižgan)

cMind pošilja **pravi lokalni LLM, ki teče v postopku** prek
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (kompaktni model obuke, kot je
Phi-3-mini). Ne potrebuje **ključa API in brez eksterne storitve**, in ob prvem zagonu — ko ni
ponudnika nastavljen in vrata belo oznake to dovolijo — je **sejan in avtomatično aktiviran**, torej
ima vsaka implementacija delujočo AI iz škatle.

- Direktorij modela (`genai_config.json` + tokenizator + teža) je nastavljena z `App:Ai:BuiltIn:ModelPath`
  (privzeto `models/onnx`, relativno na bazo aplikacije). Ko so datoteke modela odsotne ponudnik
  **degradira na tipiziran pristop z namigonom namestitve** — nikoli ne vrže, in preostala aplikacija
  ni prizadeta.
- Napaja vsako besedilno značilnost AI. Ker je kompaktni model besedilo samo (brez serverske
  spletnega iskanja ali vida) in generiranje je serializirano (ena instanca modela, ponovno
  rabljena po lenivem natovarjanju).
- **Več vgrajenih modelov se lahko sočasno nahaja.** Vsak preneseni model se nahaja pod `ModelPath/<key>`; kurirana
  katalog (Phi-3.5-mini privzeto, plus Phi-3-mini-128k) se lahko prenese in preklopi iz **Nastavitve → AI**. Izbira
  vgrajenega pomodela naloži v postopku. Pridobite/pakete model: poglejte [Ponudniki AI → vgrajeni](../deployment/ai-providers.md#vgrajeni-lokalni-ai-onnx-odpremljeni).

## Nadzori belo oznake

Implementacija belo oznake omeji AI prek `App:Branding` (uvedena na strežniku pri vsakem upsertanju
ponudnika):

- `AllowBuiltInAi` (privzeto `true`) — nastavite `false` na **odstranjenje vgrajeni modela** v
  celoti.
- `AllowLocalProviders` (privzeto `true`) — nastavite `false` za prepovedovanje lokalnih/lastnih
  končnih točk (povratna zanajka / zasebna OpenAI-kompatibilna, npr. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (privzeto prazno = vse) — seznam samo tistih vrst, ki jih
  implementacija sankcionira (npr. `["Anthropic","OpenAiCompatible"]`) za zaklučanje kateri
  ponudniki jih smejo dodati.
- `AllowAiModelManagement` (privzeto `true`) — nastavite `false` za skriovanje **pregledovanja modelov**, **izbirnika modelov na stran** in **vezave modelov na značilnost**. Vsi so upravičiteljna lastnika ob času izvajanja iz **Nastavitve → Uvrstitev** (prekriti živahno na `IOptionsMonitor`) in katalogizirana v `WhiteLabelCatalog`.

## Razširjanje: prihodnji vgrajeni modeli

Raven AI je **adapter-osnovna in zgrajena za rast**. Vsak ponudnik je `IAiProvider` izbran z
`AiProviderKind`; pantomima za značilnosti (`IAiClient`/`AiFeatureService`) se nikoli ne spremeni.
Dodajanje novega časa izvajanja vgrajenega modela pozneje (drugega modela ONNX, drugega
v-postopka motorja, GGUF/llama.cpp v-proc, itd.) je lokaliziran sprememba: dodajte `AiProviderKind`,
izvedite eno `IAiProvider` adapter, ga registrirajte, in (neobvezno) vodite privzeto sejanje +
napravo pogovora — brez značilnosti, končne točke ali spremembe orodja MCP. Vgrajeni ONNX ponudnik
je referenčna izvedba tega vzorca.

## Zmožnosti

- **Izbira modela na stran** — vsaka stran značilnosti AI in dialog prikazuje **izbirnik modela**, ki navaja modele, ki jih lahko uporabljate (vaši lastni ponudniki + privzete implementacije). Pred-izbira vezavo modela, ki je shranjena za značilnost, če je nastavljena, sicer **privzeti** model, in model, ki ga izberete, velja za to eno dejanje (poslan kot `?modelId=` in nujen s `RoutingAiClient` za ta klic). Skrit, ko implementacija onemogoči upravljanje modelov.
- **Brskajte in izberite modele, po značilnosti** — brskajte po modelih, ki jih končna točka ponudnika oglašuje (`GET /v1/models` na LM Studio / Ollama / vLLM / llama.cpp, ali vgrajeni katalog) namesto ročnega tipkanja ID, in **vsaki značilnosti AI vezati drugačen model** tako da več modelov služi različnim značilnostim hkrati (nevezana značilnost se vrne na privzeti model obsega).
- **Gradnja cBota** — delavnica na osnovi projekta na `/ai/build`: **ustvarite novega cBota** (edinstveno ime + jezik) ali **izboljšajte obstoječega**, ki ima izvorno kodo, nato **klepetajte** z modelom na `/ai/build/{projectId}` in napišite ter redefinirajte njegovo kodo. **Vsak poziv in odgovor modela je obstojni s časovnimi žigi** in preživi krmarjenje/ponovno nalaganje; izvorna koda modela se uporabi za projekt v vsaki spremembi. **Zgradite** in **Zaženite** cBota z iste strani (ali ga odprite v polnem urejevalniku). Vsak projekt se pojavi na seznamu z **časom zadnje spremembe** in kontrolami pogleda/brisanja.
- **Optimizacija parametra** — zaprta zanka: AI predlaga komplete parametrov, vsak obstojni +
  testirani čez vozlišča (`optimize-run` / `optimize-params`).
- **Avtonomni agent za portfelj** — predlogi z naročilom z celotnim dnevnikom odločitve (`AgentMandate` →
  `AgentProposal`).
- **Nastopajuči varnostni varuh** — `AiRiskGuard` storitev v ozadju oceni tečeče bote, lahko
  **samodejno ustaviti** o kritičnem tveganju (naročnik).
- **Varuh izpostave za lastnino** — omejitve narihtanja/izpostave s samodejnim izravnanjem.
- **Obvestila trga** — motor `AlertRule` z AI sentimentom (spletno iskanje ukoreninjeno, kjer ponudnik
  podpira).
- **Analiza** — pregled cBota, analiza testiranja, autopsije, sentiment trga, oblikovanje vida na
  grafikonu, kuracija tržišč.

## Površine

- Spletne končne točke pod `/api/ai/*` (klepet za gradnjo cBota `build/{id}/prompt` + `build/{id}/messages`, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …). Vsaka končna točka značilnosti sprejme neobvezno `?modelId=<credential>` za togo to eno klico na izbrani model. Plus **odkrivanje modelov** (`/api/ai/models/probe`, `/api/ai/usable-models`) in **vezave po značilnosti** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`). Projekti cBota, gradnja in pogon ponovno uporabijo končne točke graditelja (`/api/builder/projects…`).
- Orodja MCP (`AiTools`) za odjemalce AI — poglejte [mcp.md](mcp.md). Izbor ponudnika je
  transparenten za odjemalce MCP.
- **AI** navigacijska skupina — ena stran Blazor **na značilnost**: Gradnja cBota (`/ai/build`), Pregled (`/ai/review`), Razprava (`/ai/debate`), Sentiment trga (`/ai/sentiment`), Preverjanje izpostave (`/ai/exposure`), Povzetek portfelja (`/ai/digest`), Svetovalec Tune (`/ai/tune`), Optimizacija (`/ai/optimize`), skupaj Agent portfelja, Obvestila, Ključi MCP. Strani delijo `AiFeaturePageBase` + `AiOutputPanel` + `AiModelSelect`; vsaka prikazuje `AiFeatureNotice`, ko ni ponudnika nastavljenega.
- **Nastavitve → AI** (`/settings/ai`, samo lastnik) — seznam ponudnika z **Dodaj / uredi dialogom ponudnika** (vrsta, osnovna URL z namigom na vrsto, vključno s prednastavki na eno klic **Kimi/Moonshot**, Ollama in LM Studio, model, neobvezni ključ, stikala zmožnosti, "nastavi aktivno") in gumbom **Test povezanosti**.

## Konfiguracija

`App:Ai` podpira tako starega enega ključa kot tudi sejanje večponudnika:

- Starega: `ApiKey`, `Model` (privzeto `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — še čaščena kot
  privzeti ponudnik Anthropic.
- Večponudnika: `ActiveProvider` (vrsta) in `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — uvoženi v shranjevalniku ob zagonu, če še ni poverenja, torej
  lahko ekipa na Internetu pošlje nastavljeno (vključno z lokalnim-LLM) implementacijo čisto prek
  appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` nespremenjena. Za preskuse/razvoj,
ključ konfiguracije živijo v enotni [datoteki pooblastil za razvoj](../testing/dev-credentials.md)
pod `Ai`.

## Premoženost

Ponudnik je obravnavan kot zanesljiv — nič, kar počne, ne more sesediti aplikacije. To drži enako za
oblačne in lokalne končne točke (mrtev Ollama se ponovno poskuša, nato degradira točno kot
reguliran Anthropic):

- **Graciozna degradacija.** Vsaka napaka (brez ponudnika, HTTP 4xx/5xx/429, timeout, malformed telo,
  prazna vsebina, nepodprta zmožnost) vrne tipiziran `AiResult.Fail(reason)` — odjemalec nikoli ne
  vrže na stran, orodje MCP ali nastanitveno storitev.
- **Vodovod premoženosti.** `AddAiHttpClient` daje enemu skupnem AI `HttpClient` omejeno
  ponovno poskušanje pri prehodnih 5xx / napakah omrežja (eksponencialni backoff + jitter) skupaj
  z velikodušnim poskusom in skupno timeout (`AiHttp`), ponovno rabljenim s strani vsakega
  adapterja.

## Testiranje z lažnim lokalnim LLM

Raven AI je dokazana od konca do konca **brez katere koli eksterne odvisnosti** s `FakeLocalLlmServer`
— majhna v-postopka **OpenAI-kompatibilna** končna točka, ki vrne determinističen konzervirani odgovor,
žično identičen Ollama/LM Studio/vLLM. Ga podkrajuje:

- **Enota** — na adapter zahtevo-prevod + odziv-razčleni teste, usmeritev/zmožnost degradacijo.
- **Integracija** — OpenAI-kompatibilen adapter od konca do konca, parametrizirano teorijo
  premoženosti čez vsak adapter in **orodja MCP AI**.
- **E2E** — `AiLocalFixture` zaganja aplikacijo, ki kaže na lažni strežnik (ali **pravi** ponudnik,
  kadar razvijalec nastavi `AI_E2E_BASEURL` (+ neobvezni `AI_E2E_API_KEY` / `AI_E2E_KIND` /
  `AI_E2E_MODEL`) — prava poverenja zmaga) in poganja vsako značilnost AI prek realnega vmesnika. Dodajanje
  ali spreminjanje kakršne koli značilnosti AI **zahteva** E2E preskus prek te naprave (poglejte
  obveznost preskusa repa). Naročilna proga (`AI_LOCAL_LLM=1`) tečuje en pravi zaključek prek
  **Ollama** Testcontainerja.

## Vgrajeni lokalni AI — nič-nastave privzeto

Vgrajeni ONNX lokalni LLM deluje izven škatle: ko je njegov direktorij modelov odsoten in
`App:Ai:BuiltIn:AutoDownload` je `true` (privzeto), aplikacija prenese model enkrat v
ozadju iz `App:Ai:BuiltIn:DownloadBaseUrl`. Medtem ko poteka prenos, klic AI (in **preskus
povezave** v Nastavitve → AI) vrnejo jasno sporočilo »model se prenaša (prvi-čas nastavka)«
namesto trdega neuspeha. Prezračeni/merilni implementaciji nastavite `AutoDownload=false` in
pred-oskrba direktorij modela (`App:Ai:BuiltIn:ModelPath`). Vrata belo oznake
`App:Branding:AllowBuiltInAi` še vedno veljajo.

Prenos je tudi **pred-ogrevan ob zagonu** ko je vgrajeni model aktivni ponudnik, torej je
pripravljen pred prvim klikom AI namesto da bi z njim propadel »prenašam…«. **Nastavitve → AI**
površine stanja živega namestitve na kartici vgrajenega ponudnika — *Model pripravljen* / *Prenašam model…* /
*Model ni nameščen* / *Prenos ni uspel* — z gumbom **Prenesite model** (ali **Poskusite ponovno prenos**) ki
sproži enkratni prenos v ozadju na zahtevo (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`).
Omogočanje vgrajenega ponudnika iz Nastavitve ponovno uporabi že sejan red namesto dodajanja dvojnika,
zato nikoli ne pride do konflikta z enim-aktivnim-ponudnikom omejitev.
