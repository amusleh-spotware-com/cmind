---
description: "cMind AI je nezávislé od poskytovateľa — Anthropic, OpenAI, Azure OpenAI, Google Gemini a akýkoľvek OpenAI-kompatibilný koncový bod vrátane lokálnych modelov (Ollama, LM Studio, vLLM). Vyberte poskytovania, model a koncový bod; každá funkcia AI funguje nezmenená."
---

# Funkcie AI

Vrstva AI cMind je **nezávislá od poskytovateľa**. Každá funkcia hovorí jedinou neutrálnou rozhrania
(`IAiClient.CompleteAsync`); klient **smerovacieho** rieši poverenie aktívneho poskytovateľa a odposielá
na zodpovedajúci adaptér drôtu. Vyberiete si poskytovania + model + koncový bod (a, ak poskytovateľ potrebuje,
kľúč); každá existujúca funkcia funguje nezmenená s rovnakými výstupmi, šifrovaním, odolnosťou a
degradáciou.

**Batérie zahrnuté:** **vstavaný lokálny LLM sa dodáva s aplikáciou a je povolený štandardne**
(Microsoft.ML.OnnxRuntimeGenAI, napr. Phi-3.5-mini) — takže každé nasadenie má pracovný AI **bez API kľúča
a bez externej služby**. White-label nasadenie ho môže odstrániť a obmedziť, ktorých poskytovateľov môžu používatelia
pridať. Mimo vstavaného, pripojte akéhokoľvek externého poskytovania.

Podporovaní poskytovatelia:

- **Vstavaný lokálny AI** (`BuiltInOnnx`) — in-process ONNX GenAI model, bez kľúča, dodaný + štandardne zapnutý.
- **Anthropic** (Claude — Messages API)
- **OpenAI** a **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Akýkoľvek OpenAI-kompatibilný koncový bod**, vrátane **lokálnych modelov** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) a OpenAI-kompatibilných oblakov (**Kimi / Moonshot** na
  `https://api.moonshot.ai/v1/`, OpenRouter, Groq, Together, Mistral, DeepSeek) — všetko cez jeden
  OpenAI-kompatibilný adaptér, líšiace sa iba základnou URL + model + kľúč. Dialóg Pridať poskytovateľa ponúka
  **jednoklikové predvoľby** (Kimi, OpenAI, OpenRouter, Groq, DeepSeek, Mistral, Ollama, LM Studio), ktoré vyplnia
  základnú URL + ukážkový model.

Presne **jeden** poskytovať je aktívny naraz. Poverenia sú uložené **zašifrované**
(`AiProviderCredential` agregát + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
lokálny koncový bod potrebuje **žiadny kľúč**. S **bez** aktívneho poskytovania, každá funkcia vracia zakázaný
výsledok a zvyšok aplikácie beží nezmenený (bez kľúča na budovanie, testovanie alebo spustenie platformy).

**Back-compat:** existujúce nasadenie dedičného `App:Ai:ApiKey` (alebo staré šifrované `ai.api_key`
nastavenie) je automaticky vážené ako štandardne aktívny **Anthropic** poskytovať — nula akcií nepotrebná.

AI nenastavené → stránky AI stlmenia akcie a zobrazovanie transparentného pasu plus one-time výzvy na pridanie poskytovania v
**Nastavenia → AI** (`AiFeatureNotice`). Stav na `GET /api/ai/status` (`{ enabled, kind, model }`);
poskytovatelia spravovaní (iba vlastník) cez `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}` a `POST /api/ai/providers/test` pripojovateľnosť ping.

## Nasadenie štandardne vs vlastný poskytovať používateľa

Poverenia AI majú dva rozsahy:

- **Nasadenie štandardne (spravované vlastníkom).** Vlastník nakonfiguruje poskytovania (alebo dodá jeden cez
  `App:Ai:Providers[]` / dedičný `App:Ai:ApiKey`). Stane sa **zdieľaným štandardne pre každého používateľa** —
  takže makléř alebo poskytovať hostingu môže financovať AI pre všetkých svojich používateľov s **bez nastavenia na používateľa a bez
  na jednotlivca limit**. Spravované cez iba vlastníka `/api/ai/providers` cesty vyššie.
- **Vlastný poskytovať používateľa (samoobslužný).** Ktorýkoľvek prihlásen používateľ môže pridať svoj vlastný poskytovať pod
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Keď je prítomný, ich **vlastný aktívny poskytovať prepísaní nasadení
  štandardne pre ich vlastné funkcie AI**; jeho odstránenie sa vracia štandardne.

**Poradie rozlíšenia** (v `AiProviderStore`, per request používateľ): vlastný aktívny poverenie používateľa → 
nasadení štandardne → dedičný kľúč konfigurácie → žiadny (AI zakázaný). Presne jedno poverenie je aktívne
**na rozsah** (čiastočný unikátny index na `OwnerUserId`), a každý rozsah sa riešil nezávisle, takže
používateľ aktivujúci svoj vlastný kľúč nikdy nenarušuje zdieľanú štandardne. Pozadie/non-Web kontexty (bez request
používateľ) vždy rieši nasadení štandardne.

## Matica schopností poskytovateľa

Schopnosti štandardne na poskytovania a sú ovládateľné vlastníkom. Keď je schopnosť vypnutá funkcia
**degraduje, nikdy nehádzať**: web vyhľadávanie je ticho zrušené; videnie vracia typované
chyba-nepodporovaná schopnosť.

| Poskytovať | Typ | Štandardná základná URL | Kľúč potrebný | Web vyhľadávanie | Videnie | Poznámky |
|---|---|---|---|---|---|---|
| Vstavaný lokálny AI | `BuiltInOnnx` | n/a (in-process) | nie | ✖ | ✖ | dodaný ONNX GenAI model, štandardne zapnutý |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | áno | ✅ | ✅ | Messages API, `web_search` nástroj |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | áno | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | áno | ✅ | ✅ | cestu nasadenia + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | áno | ✅ | ✅ | `generateContent`, `google_search` uzemenie |
| Ollama (lokálne) | `OpenAiCompatible` | `http://localhost:11434/v1/` | nie | ✖ | závisleé modelu | cez OpenAI-kompatibilný adaptér |
| LM Studio (lokálne) | `OpenAiCompatible` | `http://localhost:1234/v1/` | nie | závisleé modelu | závisleé modelu | cez OpenAI-kompatibilný adaptér |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | vaša osluhovosť URL | nie | ✖ | závisleé modelu | cez OpenAI-kompatibilný adaptér |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | URL poskytovateľa | áno | ✖ | závisleé modelu | cez OpenAI-kompatibilný adaptér |

Úplné sprievodcoch nastavení poskytovania (kľúče, URL, ID modelov, kroky UI): pozrite si
[Poskytovatelia AI — katalóg nastavení](../deployment/ai-providers.md).

## Vstavaný lokálny AI (dodaný, štandardne zapnutý)

cMind dodáva **reálny lokálny LLM, ktorý beží in-process** cez
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (kompaktný instruct model, ako je
Phi-3.5-mini). Potrebuje **žiadny API kľúč a žiadnu externú službu**, a pri prvom spustení — keď nie je žiadny poskytovať
nakonfigurovaný a white-label brána to dovoľuje — to je **osemené a aktivované automaticky**, takže každé
nasadenie má pracovný AI hneď z krabice.

- Adresár modelu (`genai_config.json` + tokenizer + váhy) je nakonfigurovaný podľa
  `App:Ai:BuiltIn:ModelPath` (štandardne `models/onnx`, relatívne k základnému adresáru aplikácie). Keď sú súbory modelu
  absentní poskytovať **degraduje na typovanú chybu s hintom inštalácie** — nikdy nehádzať,
  a zvyšok aplikácie nie je ovplyvnený.
- Napája každú textovú funkciu AI. Že je kompaktný model, to je iba text (bez server-side web vyhľadávania alebo
  videnia) a generácia je serializovaná (jedna inštancia modelu, opätovne použitá po lazy load).
- **Viacero vstavaných modelov môže existovať spolu.** Každý stiahnutý model sa nachádza pod `ModelPath/<key>`; kuratovaný katalóg (Phi-3.5-mini štandardne, plus Phi-3-mini-128k) sa dá stiahnuť a prepnúť z **Settings → AI**. Výber vstavateľného podmodelu ho načítava in-process. Získajte/zabaľte model: pozrite si [Poskytovatelia AI → vstavaný](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## Ovládania white-label

White-label nasadenie obmedzuje AI cez `App:Branding` (vynútené na strane servera na každom upsert poskytovateľa):

- `AllowBuiltInAi` (štandardne `true`) — nastavte `false` na **odstránenie vstavaného modelu** úplne.
- `AllowLocalProviders` (štandardne `true`) — nastavte `false` na zákaz lokálnych/vlastných koncových bodov (loopback /
  privátne OpenAI-kompatibilný, napr. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (štandardne prázdne = všetky) — zoznam iba typy nasadenie sankcionuje (napr.
  `["Anthropic","OpenAiCompatible"]`) na uzamknutie, ktorí poskytovatelia môžu používatelia pridať.
- `AllowAiModelManagement` (štandardne `true`) — nastavte `false` na skrytie **prehľadu modelov**, **per-page model
  selector**, a **per-feature model binding**. Všetky sú tuneable vlastníkom za bahu z **Nastavenia →
  Nasadenie** (overlaid live na `IOptionsMonitor`) a katalógovaní v `WhiteLabelCatalog`.

## Rozšírenie: budúce vstavaných modelov

Vrstva AI je **na adaptéroch založená a postavená na rast**. Každý poskytovať je `IAiProvider` vybraný podľa
`AiProviderKind`; funkcia-čelí rozhrania (`IAiClient`/`AiFeatureService`) nikdy zmeny. Pridávanie nového
vstavaného modelového runtime neskôr (iný ONNX model, iný in-process engine, GGUF/llama.cpp
in-proc, atď.) je lokalizovaná zmena: pridajte `AiProviderKind`, implementujte jeden `IAiProvider` adaptér,
zaregistrujte ho a (voliteľne) drôtový štandardný seeding + možnosť dialógu — žiadna funkcia, koncový bod, alebo MCP nástroj
zmeny. Vstavaný ONNX poskytovať je referenčná implementácia tohto vzoru.

## Schopnosti

- **Vytvorenie cBot** — plain-English prompt → runnable cBot cez **generate → build → AI-fix** samoreparácia loop (`build-strategy`), na `/ai/build`. **Generovaný zdrojový kód sa zobrazuje** keď je build hotov (s tlačidlom kopírovania), spolu so skriptom buildu (tiež kopírovateľný) — pri úspechu *a* pri zlyhání. **Aj neúspešný build sa uloží do vašich cBotov** (s skutočným jedinečným názvom) a ponúka odkaz *Otvoriť v editore*, aby ste mohli opraviť chyby kompilácie a znovu vytvoriť, namiesto straty práce.
- **Per-page model selection** — každá stránka funkcie AI a dialóg ukazuje **model selector** listujúcu modely, ktoré môžete použiť (vaši vlastní poskytovatelia + nasadení štandardy). Pré-vyberie viazanie uložené funkcie, ak je nastavené, ináč **štandardný** model, a model, ktorý vyberiete, sa vzťahuje na túto jednu akciu (poslané ako `?modelId=` a vynútené `RoutingAiClient` pre to volanie). Skryté, keď nasadení vypnutie správu modelov.
- **Prehliadajte a vyberte si modely, per feature** — browse modelov čo poskytovateľ-koncový bod advertise (`GET /v1/models` na LM Studio / Ollama / vLLM / llama.cpp, alebo vstavaný katalóg) namiesto hand-typing id, a **viažte každú funkciu AI k inému modelu** aby niekoľko modelov slúžilo rôznym funkciám naraz (unbind funkcia falls back k scope's default poskytovateľ).
- **Optimalizácia parametrov** — uzavretá slučka: AI navrhuje sady param, každý trvalý + backtestovaný naprieč uzlami (`optimize-run` / `optimize-params`).
- **Autonómny agent portfólia** — mandátované návrhy s úplným rozhodovacím denníkom (`AgentMandate` → `AgentProposal`).
- **Pôsobiaci strážca rizika** — `AiRiskGuard` background služba posudzuje bežiace boty, môžu **auto-stop** na kritické riziko (opt-in).
- **Strážca expozície prop-firm** — pokles/expozičný limit s auto-flatten.
- **Upozornenia na trh** — `AlertRule` engine s AI sentimentom (web-search založené, kde poskytovať podporuje).
- **Analýza** — recenzia cBot, analýza backtestingu, post-mortemy, sentiment trhu, chart-vision dizajn, kurácia trhov.

## Povrchy

- Web koncové body pod `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …). Každý koncový bod funkcie akceptuje voliteľný `?modelId=<credential>` na spustenie tohto jedného volania na vybranom modeli. Plus **objav modelov** (`/api/ai/models/probe`, `/api/ai/usable-models`) a **per-feature viazania** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- MCP nástroje (`AiTools`) pre AI klientov — pozrite si [mcp.md](mcp.md). Výber poskytovateľa je transparentný pre MCP klientov.
- **AI** skupinou navigácie — jeden Blazor **stránka na funkciu**: Vytvorenie cBot (`/ai/build`), Recenzia (`/ai/review`), Debate (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Kontrola expozície (`/ai/exposure`), Portfolio Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimalizovať (`/ai/optimize`), plus Portfolio Agent, Alerts, MCP Keys. Stránky zdieľanie `AiFeaturePageBase` + `AiOutputPanel` + `AiModelSelect`; každý ukazuje `AiFeatureNotice` keď nie je žiadny poskytovať nakonfigurovaný.
- **Nastavenia → AI** (`/settings/ai`, iba vlastník) — zoznam poskytovateľov s **Pridajte / upravte dialóg poskytovateľa** (typ, základná URL s per-kind hints a **jednoklikové predvoľby** vrátane **Kimi/Moonshot**, Ollama a LM Studio, model, voliteľný kľúč, prepínače schopností, "nastaviť ako aktívne") a **Testovať pripojenie** tlačidlo.

## Konfigurácia

`App:Ai` podporuje dedičný jediný kľúč a multi-poskytovateľ seeding:

- Dedičný: `ApiKey`, `Model` (štandardne `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — stále vážené ako
  štandardne Anthropic poskytovať.
- Multi-poskytovať: `ActiveProvider` (typ) a `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — importované do úložiska pri spustení, ak žiadne poverenia ešte neexistujú, takže ops
  tím môže dodať nakonfigurované (incl. local-LLM) nasadenie čisto cez appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` nezmenené. Pre testy/vývoj, config kľúč
živí v jednotnom [dev-credentials file](../testing/dev-credentials.md) pod `Ai`.

## Spoľahlivosť

Poskytovať sa považuje za nespoľahlivý — nič, čo robí, nemôže zobrať aplikáciu. To platí identicky
pre cloud a lokálne koncové body (mŕtva Ollama opakuje potom degraduje presne ako škrtená Anthropic):

- **Milostivá degradácia.** Každý režim zlyhania (žiadny poskytovať, HTTP 4xx/5xx/429, timeout, malformovaný body,
  prázdny obsah, nepodporovaná schopnosť) vracia typované `AiResult.Fail(reason)` — klient nikdy
  nehádzať do stránky, MCP nástroja alebo hosted služby.
- **Pipeline odolnosti.** `AddAiHttpClient` dáva jeden zdieľaný AI `HttpClient` ohraničené opakovaní na
  prechodný 5xx / network zlyhania (exponenciálny backoff + jitter) plus štedré per-attempt a celkové
  timeouts (`AiHttp`), opätovne používané každý adaptér.

## Testovanie s falošným lokálnym LLM

Vrstva AI je dokázaná end-to-end **bez akejkoľvek externej závislosti** podľa `FakeLocalLlmServer` — malý
in-process **OpenAI-kompatibilný** koncový bod vrátenie deterministickej konzervovanej odpovede, drôtovo-identické k
Ollama/LM Studio/vLLM. Záloha:

- **Jednotka** — per-adapter request-translation + response-parse testy, smerovanie/schopnosť degradácia.
- **Integrácia** — OpenAI-kompatibilný adaptér end-to-end, parametrizovaná resilience teória cez
  každý adaptér a **MCP AI nástroje**.
- **E2E** — `AiLocalFixture` bootstrapy aplikácia nasmerovaná na falošný server (alebo **reálny** poskytovať keď
  vývojár nastaví `AI_E2E_BASEURL` (+ voliteľne `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  reálne poverenia vyhrávajú) a spúšťa každú funkciu AI cez reálny UI. Pridávanie alebo zmena akejkoľvek funkcie AI
  **vyžaduje** E2E test cez túto armatúru (pozrite si test mandát repo). Opt-in pás
  (`AI_LOCAL_LLM=1`) spúšťa jeden reálny completion cez **Ollama** Testcontainer.

## Vstavaný lokálny AI — nulové nastavenie štandardne

Vstavaný ONNX lokálny LLM funguje hneď z krabice: keď adresár modelu chýba a
`App:Ai:BuiltIn:AutoDownload` je `true` (štandardne), aplikácia stiahne model raz v
pozadí z `App:Ai:BuiltIn:DownloadBaseUrl`. Kým sťahovanie beží, AI-volania (a **Test
connection** v Nastavenia → AI) vrátia jasné "model sa sťahuje (úplne prvýkrát nastavenie)" správu
namiesto hard failure. Air-gapped/metered nasadenia nastaviť `AutoDownload=false` a
pre-provision adresár modelu (`App:Ai:BuiltIn:ModelPath`). White-label
`App:Branding:AllowBuiltInAi` brána stále platí.

Sťahovanie je tiež **predbežne prehrievané pri spustení**, keď je vstavaný model aktívnym poskytovateľom, takže je pripravený pred prvým kliknutím AI namiesto zlyhania tohto kliknutia s „sťahovávaním…". **Nastavenia → AI** zobrazuje stav live inštalácie na karte vstavaného poskytovateľa — *Model je pripravený* / *Sťahovanie modelu…* / *Model nie je nainštalovaný* / *Sťahovanie zlyhalo* — s tlačidlom **Sťahovať model** (alebo **Znova skúsiť sťahovanie**), ktoré spúšťa jednorazové pozadie sťahovanie na požiadavku (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`). Povolenie vstavaného poskytovateľa z Nastavení opätovne používa už nasemenený riadok namiesto pridávania duplikátu, takže sa nikdy nedostane do konfliktu s obmedzením jediného aktívneho poskytovateľa.
