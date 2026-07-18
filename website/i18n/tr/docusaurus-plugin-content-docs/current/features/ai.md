---
description: "cMind AI sağlayıcıdan bağımsızdır — Anthropic, OpenAI, Azure OpenAI, Google Gemini ve yerel modeller dahil (Ollama, LM Studio, vLLM) herhangi bir OpenAI uyumlu uç nokta. Bir sağlayıcı, model ve uç nokta seçin; her AI özelliği değişmeden çalışır."
---

# AI özellikleri

cMind'in AI katmanı **sağlayıcıdan bağımsızdır**. Her özellik tek bir sağlayıcı-tarafsız dikişle
(`IAiClient.CompleteAsync`) konuşur; bir **yönlendirme istemcisi** aktif sağlayıcı kimlik bilgisini çözer
ve eşleşen wire adaptörüne gönderir. Bir sağlayıcı + model + uç nokta (ve sağlayıcının ihtiyacı varsa
bir anahtar) seçersiniz; her mevcut özellik aynı geçitleme, şifreleme, dayanıklılık ve bozulma ile
değişmeden çalışır.

**Piller dahil:** **uygulamayla birlikte gelen ve varsayılan olarak etkin bir yerleşik yerel LLM**
(Microsoft.ML.OnnxRuntimeGenAI, örn. Phi-3.5-mini) — böylece her dağıtım **API anahtarı ve harici servis
olmadan** çalışan AI'ya sahiptir. Bir white-label dağıtımı bunu kaldırabilir ve kullanıcıların hangi
sağlayıcıları ekleyebileceğini kısıtlayabilir. Yerleşiğin ötesinde, herhangi bir harici sağlayıcı bağlayın.

Desteklenen sağlayıcılar:

- **Yerleşik yerel AI** (`BuiltInOnnx`) — süreç içi ONNX GenAI modeli, anahtar yok, gönderildi + varsayılan-açık.
- **Anthropic** (Claude — Messages API)
- **OpenAI** ve **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Herhangi bir OpenAI uyumlu uç nokta**, **yerel modeller** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) ve OpenAI uyumlu bulutlar (OpenRouter, Groq, Together, Mistral,
  DeepSeek) dahil — tümü tek OpenAI uyumlu adaptör aracılığıyla, yalnızca temel URL + model + anahtar
  bakımından farklı.

Aynı anda tam olarak **bir** sağlayıcı aktiftir. Kimlik bilgileri **şifreli** olarak saklanır
(`AiProviderCredential` toplamı + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
bir yerel uç nokta **anahtar gerektirmez**. **Hiç** aktif sağlayıcı olmadığında, her özellik devre dışı
sonucu döndürür ve uygulamanın geri kalanı değişmeden çalışır (platformu derlemek, test etmek veya
çalıştırmak için anahtar gerekmez).

**Geriye dönük uyumluluk:** mevcut bir dağıtımın eski `App:Ai:ApiKey`'i (veya eski şifreli `ai.api_key`
ayarı) otomatik olarak varsayılan bir aktif **Anthropic** sağlayıcısı olarak onurlandırılır — sıfır eylem
gerekir.

AI yapılandırılmadı → AI sayfaları eylemleri karartır ve bir banner artı **Settings → AI**'de
(`AiFeatureNotice`) bir sağlayıcı eklemek için tek seferlik bir istem gösterir. Durum
`GET /api/ai/status`'ta (`{ enabled, kind, model }`); sağlayıcılar (yalnızca owner) `GET/PUT /api/ai/providers`,
`POST /api/ai/providers/{id}/activate`, `DELETE /api/ai/providers/{id}` ve bir
`POST /api/ai/providers/test` bağlantı ping'i aracılığıyla yönetilir.

## Dağıtım varsayılanı ve kullanıcının kendi sağlayıcısı

AI kimlik bilgilerinin iki kapsamı vardır:

- **Dağıtım varsayılanı (owner-yönetimli).** Owner bir sağlayıcı yapılandırır (veya `App:Ai:Providers[]` /
  eski `App:Ai:ApiKey` aracılığıyla birini gönderir). Bu, **her kullanıcı için paylaşılan varsayılan**
  olur — böylece bir broker veya barındırma sağlayıcısı, tüm kullanıcıları için **kullanıcı başına kurulum
  ve kullanıcı başına limit olmadan** AI'yı finanse edebilir. Yukarıdaki yalnızca-owner `/api/ai/providers`
  rotaları aracılığıyla yönetilir.
- **Bir kullanıcının kendi sağlayıcısı (self-servis).** Giriş yapmış herhangi bir kullanıcı,
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}` altında kendi sağlayıcısını ekleyebilir. Mevcut olduğunda, **kendi
  aktif sağlayıcıları kendi AI özellikleri için dağıtım varsayılanını geçersiz kılar**; onu kaldırmak
  varsayılana geri döner.

**Çözümleme sırası** (`AiProviderStore`'da, istek başına kullanıcı): kullanıcının kendi aktif kimlik
bilgisi → dağıtım varsayılanı → eski yapılandırma anahtarı → hiçbiri (AI devre dışı). **Kapsam başına**
tam olarak bir kimlik bilgisi aktiftir (`OwnerUserId` başına bir kısmi benzersiz indeks) ve her kapsam
bağımsız olarak çözülür, böylece kendi anahtarını etkinleştiren bir kullanıcı asla paylaşılan varsayılanı
bozmaz. Arka plan/Web olmayan bağlamlar (istek kullanıcısı yok) her zaman dağıtım varsayılanını çözer.

## Sağlayıcı yetenek matrisi

Yetenekler sağlayıcı başına varsayılan olarak ayarlanır ve owner tarafından geçersiz kılınabilir. Bir
yetenek kapalı olduğunda özellik **bozulur, asla fırlatmaz**: web araması sessizce bırakılır; vision
tipli bir yetenek-desteklenmiyor başarısızlığı döndürür.

| Sağlayıcı | Kind | Varsayılan temel URL | Anahtar gerekli | Web araması | Vision | Notlar |
|---|---|---|---|---|---|---|
| Yerleşik yerel AI | `BuiltInOnnx` | n/a (süreç içi) | hayır | ✖ | ✖ | gönderilen ONNX GenAI modeli, varsayılan-açık |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | evet | ✅ | ✅ | Messages API, `web_search` aracı |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | evet | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | evet | ✅ | ✅ | deployment yolu + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | evet | ✅ | ✅ | `generateContent`, `google_search` topraklama |
| Ollama (yerel) | `OpenAiCompatible` | `http://localhost:11434/v1/` | hayır | ✖ | modele bağlı | OpenAI uyumlu adaptör aracılığıyla |
| LM Studio (yerel) | `OpenAiCompatible` | `http://localhost:1234/v1/` | hayır | modele bağlı | modele bağlı | OpenAI uyumlu adaptör aracılığıyla |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | sunduğunuz URL | hayır | ✖ | modele bağlı | OpenAI uyumlu adaptör aracılığıyla |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | sağlayıcı URL'si | evet | ✖ | modele bağlı | OpenAI uyumlu adaptör aracılığıyla |

Sağlayıcı başına tam kurulum kılavuzları (anahtarlar, URL'ler, model id'leri, UI adımları): bkz.
[AI sağlayıcıları — kurulum kataloğu](../deployment/ai-providers.md).

## Yerleşik yerel AI (gönderildi, varsayılan-açık)

cMind, [Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) aracılığıyla **süreç içinde
çalışan gerçek bir yerel LLM** (Phi-3.5-mini gibi kompakt bir instruct modeli) gönderir. **API anahtarı ve
harici servis gerektirmez** ve ilk başlangıçta — hiçbir sağlayıcı yapılandırılmadığında ve white-label
geçidi buna izin verdiğinde — **otomatik olarak seed edilir ve etkinleştirilir**, böylece her dağıtım
kutudan çıktığı gibi çalışan AI'ya sahiptir.

- Model dizini (`genai_config.json` + tokenizer + ağırlıklar) `App:Ai:BuiltIn:ModelPath` (varsayılan
  `models/onnx`, uygulama temel dizinine göre) tarafından yapılandırılır. Model dosyaları yoksa sağlayıcı
  **bir kurulum ipucuyla tipli bir başarısızlığa bozulur** — asla fırlatmaz ve uygulamanın geri kalanı
  etkilenmez.
- Her metin AI özelliğini destekler. Kompakt bir model olduğu için yalnızca metindir (sunucu tarafı web
  araması veya vision yok) ve üretim serileştirilir (bir model instance'ı, tembel bir yükleme sonrası
  yeniden kullanılır).
- **Yerleşik modeller birlikte var olabilir.** Her indirilen model `ModelPath/<key>` altında yaşar; kuratörlü bir katalog (Phi-3.5-mini varsayılanı, artı Phi-3-mini-128k) indirilip **Settings → AI**'den değiştirilebilir. Yerleşik bir alt modeli seçmek onu süreçte yükler. Modeli edinin/paketleyin: bkz. [AI sağlayıcıları → yerleşik](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## White-label kontrolleri

Bir white-label dağıtımı AI'yı `App:Branding` aracılığıyla kısıtlar (her sağlayıcı upsert'inde sunucu
tarafında zorlanır):

- `AllowBuiltInAi` (varsayılan `true`) — yerleşik modeli tamamen **kaldırmak** için `false` ayarlayın.
- `AllowLocalProviders` (varsayılan `true`) — yerel/kendi-barındırılan uç noktaları (loopback /
  özel OpenAI uyumlu, örn. Ollama/LM Studio/vLLM) yasaklamak için `false` ayarlayın.
- `AllowedAiProviderKinds` (varsayılan boş = tümü) — kullanıcıların hangi sağlayıcıları ekleyebileceğini
  kilitlemek için yalnızca dağıtımın onayladığı türleri listeleyin (örn. `["Anthropic","OpenAiCompatible"]`).
- `AllowAiTasks` (varsayılan `true`) — **arka plan AI görevi** özelliğini **kaldırmak** için `false` ayarlayın (`/ai/tasks`
  sayfası ve görev API 404 döndürür; runner talepleri durdurur); eş zamanlı AI özellikleri yine de çalışır.
- `AllowAiModelManagement` (varsayılan `true`) — **model taraması** ve **özellik başına model bağlama**
  **gizlemek** için `false` ayarlayın. Her ikisi de **Settings → Deployment**'ten owner tarafından çalışma zamanında
  ayarlanabilir (canlı olarak `IOptionsMonitor` üzerine yerleştirilir) ve `WhiteLabelCatalog`'da kataloglanır.

## Genişletme: gelecekteki yerleşik modeller

AI katmanı **adaptör tabanlıdır ve büyümek için inşa edilmiştir**. Her sağlayıcı, `AiProviderKind`
tarafından seçilen bir `IAiProvider`'dır; özellik-tarafındaki dikiş (`IAiClient`/`AiFeatureService`)
asla değişmez. Daha sonra yeni bir yerleşik model çalışma zamanı eklemek (başka bir ONNX modeli, farklı
bir süreç içi motor, GGUF/llama.cpp süreç içi vb.) yerelleştirilmiş bir değişikliktir: bir `AiProviderKind`
ekleyin, bir `IAiProvider` adaptörü uygulayın, kaydedin ve (isteğe bağlı olarak) varsayılan seed'lemeyi +
bir iletişim kutusu seçeneğini bağlayın — özellik, uç nokta veya MCP aracı değişikliği yok. Yerleşik ONNX
sağlayıcısı bu desenin referans uygulamasıdır.

## Yetenekler

- **cBot Oluştur** — düz İngilizce prompt → **generate → build → AI-fix** kendi kendini onarma döngüsü aracılığıyla çalıştırılabilir cBot (`build-strategy`), `/ai/build`'da. **Oluşturulan kaynak kodu, derleme bittiğinde gösterilir** (kopyala düğmesi ile), derleme günlüğünün yanında — başarıda *ve* başarısızlıkta — böylece her zaman AI'ın ne yazdığını görürsünüz, sadece hataları değil.
- **Arka plan AI görevleri** — uzun çalışan bir AI işi başlatın (örn. cBot oluşturun) seçtiğiniz modelle, sonra sayfayı ayrılın ve sonuç için geri dönün. Karşılaştırmak için birden fazla model seçin — her biri kendi görevi olarak çalışır (`/ai/tasks`). Bir web-host çalışanı görevleri self-healing bir kiralama üzerinde talep eder (bir düğüm ölürse geri alınır) ve ilerlemeyi bir per-görev etkinlik günlüğüne akışla aktarır.
- **Modelleri tarayın ve seçin, özellik başına** — bir sağlayıcı uç noktası (`GET /v1/models` LM Studio / Ollama / vLLM / llama.cpp üzerinde veya yerleşik kataloğ) reklamını yaptığı modelleri tarayın, elle bir id yazarak yerine, ve **her AI özelliğini farklı bir modele bağlayın**, böylece birden fazla model aynı anda farklı özellikleri sunabilir (bağlı olmayan bir özellik kapsam'ın aktif sağlayıcısına geri döner).
- **Parametre optimizasyonu** — kapalı döngü: AI param setleri önerir, her biri node'lar arasında kalıcılaştırılır + backtest edilir (`optimize-run` / `optimize-params`).
- **Otonom portföy ajanı** — tam karar günlüğüyle mandat odaklı öneriler (`AgentMandate` → `AgentProposal`).
- **Hareket eden risk muhafızı** — `AiRiskGuard` arka plan servisi çalışan botları değerlendirir, kritik riskte **otomatik-durdurabilir** (opt-in).
- **Prop-firm maruziyet koruyucusu** — otomatik-düzleştirme ile düşüş/maruziyet limitleri.
- **Piyasa uyarıları** — AI duyarlılığı ile `AlertRule` motoru (sağlayıcının desteklediği yerlerde web-araması topraklamalı).
- **Analiz** — cBot incelemesi, backtest analizi, otopsiler, piyasa duyarlılığı, chart-vision tasarımı, marketplace küratörlüğü.

## Yüzeyler

- `/api/ai/*` altında Web uç noktaları (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …) artı **arka plan görevleri** (`/api/ai/tasks` create/list/detail/cancel/delete), **model keşfi** (`/api/ai/models/probe`, `/api/ai/usable-models`) ve **özellik başına bağlamalar** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- AI istemcileri için MCP araçları (`AiTools`) — bkz. [mcp.md](mcp.md). Sağlayıcı seçimi MCP istemcileri için şeffaftır.
- **AI** nav grubu — özellik başına bir Blazor **sayfası**: Build cBot (`/ai/build`), Review (`/ai/review`), Debate (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Exposure Check (`/ai/exposure`), Portfolio Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimize (`/ai/optimize`), **AI Tasks** (`/ai/tasks`), artı Portfolio Agent, Alerts, MCP Keys. Sayfalar `AiFeaturePageBase` + `AiOutputPanel` paylaşır; her biri hiçbir sağlayıcı yapılandırılmadığında `AiFeatureNotice` gösterir.
- **Settings → AI** (`/settings/ai`, yalnızca owner) — **Add / edit provider dialog** ile sağlayıcı listesi (kind, tür başına ipuçlarıyla temel URL bir Ollama/LM Studio localhost ön ayarı dahil, model, isteğe bağlı anahtar, yetenek geçişleri, "set active") ve bir **Test connection** düğmesi.

## Yapılandırma

`App:Ai` hem eski tek anahtarı hem de çoklu sağlayıcı seed'lemeyi destekler:

- Eski: `ApiKey`, `Model` (varsayılan `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — hâlâ varsayılan
  Anthropic sağlayıcısı olarak onurlandırılır.
- Çoklu sağlayıcı: `ActiveProvider` (kind) ve `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — henüz kimlik bilgisi yoksa başlangıçta depoya içe aktarılır, böylece bir
  ops ekibi yalnızca appsettings/env aracılığıyla yapılandırılmış (yerel-LLM dahil) bir dağıtım gönderebilir.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` değişmedi. Testler/dev için, bir yapılandırma
anahtarı birleşik [dev-credentials dosyasında](../testing/dev-credentials.md) `Ai` altında bulunur.

## Güvenilirlik

Sağlayıcı güvenilmez olarak değerlendirilir — yaptığı hiçbir şey uygulamayı çökertmez. Bu, bulut ve yerel
uç noktalar için aynı şekilde geçerlidir (ölü bir Ollama, kısıtlanmış bir Anthropic gibi tam olarak yeniden
dener sonra bozulur):

- **Zarif bozulma.** Her başarısızlık modu (sağlayıcı yok, HTTP 4xx/5xx/429, zaman aşımı, hatalı biçimli
  gövde, boş içerik, desteklenmeyen yetenek) tipli bir `AiResult.Fail(reason)` döndürür — istemci asla bir
  sayfaya, MCP aracına veya barındırılan servise fırlatmaz.
- **Dayanıklılık hattı.** `AddAiHttpClient`, tek paylaşılan AI `HttpClient`'ına geçici 5xx / ağ
  başarısızlıklarında sınırlı bir yeniden deneme (üstel geri çekilme + seğirme) artı cömert deneme başına
  ve toplam zaman aşımları (`AiHttp`) verir, her adaptör tarafından yeniden kullanılır.

## Sahte yerel LLM ile test etme

AI katmanı, `FakeLocalLlmServer` tarafından **herhangi bir harici bağımlılık olmadan** uçtan uca
kanıtlanır — deterministik bir hazır yanıt döndüren, Ollama/LM Studio/vLLM ile wire-özdeş küçük bir
süreç içi **OpenAI uyumlu** uç nokta. Şunları destekler:

- **Unit** — adaptör başına istek-çevirisi + yanıt-ayrıştırma testleri, yönlendirme/yetenek bozulması.
- **Integration** — OpenAI uyumlu adaptör uçtan uca, her adaptör arasında parametrize dayanıklılık
  teorisi ve **MCP AI araçları**.
- **E2E** — `AiLocalFixture`, uygulamayı sahte sunucuya yönlendirilmiş olarak başlatır (veya geliştirici
  `AI_E2E_BASEURL` (+ isteğe bağlı `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) ayarladığında bir
  **gerçek** sağlayıcı — gerçek kimlik bilgileri kazanır) ve her AI özelliğini gerçek UI aracılığıyla
  sürer. Herhangi bir AI özelliği eklemek veya değiştirmek, bu fixture aracılığıyla bir E2E testi
  **gerektirir** (bkz. repo test mandatı). Bir opt-in şerit (`AI_LOCAL_LLM=1`) bir **Ollama**
  Testcontainer'ı aracılığıyla bir gerçek tamamlama çalıştırır.

## Yerleşik yerel AI — varsayılan olarak sıfır kurulum

Yerleşik ONNX yerel LLM kutudan çıktığı gibi çalışır: model dizini yoksa ve
`App:Ai:BuiltIn:AutoDownload` `true` ise (varsayılan), uygulama modeli
`App:Ai:BuiltIn:DownloadBaseUrl`'den arka planda bir kez indirir. İndirme çalışırken, AI çağrıları
(ve Settings → AI'de **Test connection**) sert bir başarısızlık yerine net bir "model indiriliyor
(ilk kez kurulum)" mesajı döndürür. Hava boşluklu/ölçülü dağıtımlar `AutoDownload=false` ayarlar ve
model dizinini önceden hazırlar (`App:Ai:BuiltIn:ModelPath`). White-label
`App:Branding:AllowBuiltInAi` geçidi hâlâ geçerlidir.

İndirme ayrıca **başlangıçta önceden ısıtılmıştır** yerleşik model aktif sağlayıcı olduğunda, böylece ilk
AI tıklamasından önce hazır olup, "indiriliyor…" ile bu tıklamayı başarısız kılmak yerine. **Settings → AI**
yerleşik sağlayıcı kartında canlı kurulum durumunu yüzeylere çıkarır — *Model ready* / *Downloading model…* /
*Model not installed* / *Download failed* — model indirmeyi (veya indirmeyi **yeniden denemeyi**) istek üzerine
bir kez arka planda getiren **Download model** (veya **Retry download**) düğmesi ile (`GET /api/ai/built-in/status`,
`POST /api/ai/built-in/install`). Settings'den yerleşik sağlayıcıyı etkinleştirmek, bir kopya eklemek yerine
zaten seed yapılmış satırı yeniden kullanır, böylece asla tek-aktif-sağlayıcı kısıtlamasında çakışmaz.
