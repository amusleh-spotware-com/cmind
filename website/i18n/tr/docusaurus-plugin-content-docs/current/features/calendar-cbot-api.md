# Takvim REST & cBot API'si

Ekonomik takvim, **sürümlenmiş, JWT ile güvenli, hız-sınırlı bir REST API'si** olarak sunulur — amiral
gemisi entegrasyon yüzeyi. Herhangi bir harici hizmet, gösterge paneli veya cBot ona bir ürün olarak
entegre olur. FXStreet Calendar API'si ile özellik paritesine sahiptir ve onu geçer: zaman-noktası
`asOf`, tam revizyon zincirleri, deterministik etki gerekçesi, sürpriz analitiği, ülke→sembol çözümü ve
diğer takvim API'lerinin sunmadığı karartma matematiği.

> **Durum.** JWT güvenliği (istemci verilişi + token değişimi), kapılama ve çekirdek okuma uç noktaları
> — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`,
> `affected-symbols`, `health` — **uygulanmış ve entegrasyon testlidir** (kimlik doğrulama, kapsam
> uygulaması, özellik/beyaz-etiket 404), ayrıca **`events/batch`** (sınırlı çoğullama) ve keşfedilebilir
> bir **`/openapi.json`** belgesi, olay/geçmiş okumalarında **`ETag`/`If-None-Match` 304** ve **anahtar
> kümesi imleç sayfalaması** (`Link: rel="next"`), **SSE `stream`** (canlı `event: release` gönderimi,
> yoklama-destekli), **HMAC-imzalı web kancaları** (`X-CMind-Signature: sha256=…`, sahip-kayıtlı,
> kalıcı bir su işaretinden config-kapılı bir işçi tarafından teslim edilir) ve gönderilmiş **tipli
> istemci** (`CmindCalendarClient`). Tam genel API yüzeyi uygulanmıştır.

## Güvenlik — JWT

API, yeni bir şema değil, deponun mevcut HS256 token mekanizmasını (CtraderCliNode aracılarının
kullandığı aynı desen) yeniden kullanır:

- Bir uygulama yöneticisi bir **Calendar API istemcisi** verir (ad + kapsamlar + son kullanma). İstemci
  kimliğini ve gizli anahtarını `POST /api/calendar/v1/token`'da bir **kısa ömürlü HS256 JWT** ile
  değiştirir (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 dk, `scope` iddiası). İsteklerde
  yalnızca kısa JWT taşınır (`Authorization: Bearer <jwt>`).
- İstemci gizli anahtarı `ISecretProtector` aracılığıyla **şifreli** saklanır — asla düz metin, asla
  günlüklenmez.
- **Kapsamlar** (en az ayrıcalık): `calendar:read`, `calendar:blackout`, `calendar:surprises`,
  `calendar:stream`. Bir cBot token'ı tipik olarak yalnızca `read` + `blackout` alır.
- Standart `JwtBearer` doğrulaması (veren, izleyici kitle, ömür, imzalama anahtarı; `alg=none`
  reddedilir; sıkı saat sapması). İstemci başına token-kova hız sınırı + küresel sınırlayıcı;
  `Retry-After` ile `429`. Tüm kimlik doğrulama hataları denetlenir.
- İstemciyi devre dışı bırakmak gelecekteki token verilişini hemen durdurur; kısa JWT ömrü sızdırılmış
  bir token'ı sınırlar. Özellik devre dışı bırakıldığında tüm `/api/calendar/**` ağacı `404` verir.

## Kurallar

- **Temel yol & sürümleme:** `/api/calendar/v1/...` (URL-sürümlü; ekleyici değişiklikler artırmaz).
- **Biçim:** JSON; RFC 3339 UTC anları artı açık bir `sourceTimeZone`; isteğe bağlı `tz=`, UTC
  bağlantısını kaybetmeden bir kolaylık yerel saati oluşturur.
- **Sayfalama:** imleç-tabanlı (`cursor`, `limit` ≤ 1000); gövdede `next` imleci ve bir `Link` başlığı.
- **Önbellekleme:** `ETag` + `If-None-Match`; tarihsel aralıklar uzun bir TTL, yaklaşanlar kısa bir TTL alır.
- **Hatalar:** RFC 7807 `problem+json`, asla çıplak bir `500`.
- **Bozulmuş okumalar:** bir kaynak/DB arızası `200` en-iyi-bilinen veriyi artı bir
  `X-Calendar-Freshness` / `stale=true` sinyali döndürür (veya gerçekten hiçbir şey bilinmiyorsa yalnızca
  `503 Retry-After`) — cBot karar verir.

## Uç noktalar

| Yöntem & yol | Amaç | Anahtar parametreler |
|---|---|---|
| `POST /v1/token` | İstemci id+secret → kısa JWT değiştir | gövde: `clientId`, `clientSecret` |
| `GET /v1/events` | Bir penceredeki olaylar (yaklaşan veya tarihsel) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Bir olay: tam revizyon zinciri, sürpriz, etki gerekçesi, etkilenen semboller | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Sıralı revizyon geçmişi | — |
| `GET /v1/history` | Bir seri için derin tarihsel çekiş (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | İzlenen göstergeler kataloğu + kadans + kaynak | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Tarihsel gerçek/tahmin/sürpriz z-skoru serisi | `series`,`count`/`from,to` |
| `GET /v1/next` | Bir sembol için sonraki ilgili yayın (ülke→sembol eşlemeli) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Bir sembol şimdi/T'de yüksek-etkili bir pencerenin içinde mi | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Bir olayı → bir izleme listesindeki sembollere çöz | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Birkaç sorguyu tek gidiş-dönüşte çoğulla | gövde: sorgular dizisi |
| `GET /v1/stream` (SSE) | Canlı gönderim: yayınlar/revizyonlar/pencereye-giriş | `currencies`,`minImpact` (kapsam `calendar:stream`) |
| `POST /v1/webhooks` | Yayın/revizyon/karartma için HMAC-imzalı bir geri çağrı kaydet | gövde: url, filtreler, secret |
| `GET /v1/health` | Kaynak başına tazelik + kapsama | — |

## Karartma — cBot haber filtresi

`GET /v1/blackout`, `{ inBlackout, event, startsAt, endsAt, stale }` döndürür. Belirsizlikte
**yapılandırılmış muhafazakâr yanıta** varsayılan olarak döner (varsayılan olarak fail-closed: risk-off
botları için "karartmada varsay"), artı bir `stale` bayrağı — bir veri boşluğu asla NFP boyunca ticarete
yeşil ışık yakmaz. Uç nokta, sert bir sunucu zaman aşımı olan saf bir DB/önbellek okumasıdır; sıcak
yolda senkron kaynak çekişi yoktur.

Gönderilmiş bir tipli istemci (`Infrastructure.Calendar.CmindCalendarClient`) bunu sarar: `HttpClient`'ini
API köküne yöneltin, bir kez `GetTokenAsync(clientId, clientSecret)` çağırın, sonra her emirden önce
`GetBlackoutAsync(token, symbol)` çağırın — **yapı gereği fail-safe'tir** (herhangi bir başarısızlık veya
ayrıştırma hatası `InBlackout = true, Stale = true` döndürür, böylece bir veri boşluğu asla ticarete
yeşil ışık yakmaz). Bir cBot haberlerin etrafında şöyle duraklar:

```csharp
// Pseudocode for a cTrader cBot using WebRequest + a Calendar API client token.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries in the news window
// ...otherwise proceed to place the order
```

## Backtest'ler için zaman-noktası

Takvimi tam olarak geçmiş bir anda durduğu gibi almak için herhangi bir okumada `asOf` iletin —
gerçekler, tahminler ve revizyonlar *o zaman oldukları gibi*. `asOf` okumaları saf ve önbelleklenebilir
olduğundan, geçmişi zorlayan bir backtest her seferinde aynı baytları alır ve backtest edilmiş bir haber
kuralı tam olarak canlı olan gibi davranır (revize edilmiş değerlerden ileri-bakış yok).

## Algo çağıranlar için dayanıklılık

API bir ticaret sıcak yolunda bulunur, bu yüzden canlı bir bota asla istisna fırlatmaz: her yol
iyi-biçimlendirilmiş bir `problem+json` veya tipli bir bozulmuş gövde döndürür. Kopya-ticaretin
dayanıklılık ilkelerini yeniden kullanır — her kaynak istemcisindeki standart HTTP dayanıklılık işleyici,
kaynak başına bir alan devre kesici, başlatma uzlaştırmalı kiralama-korumalı bir tekil alım işçisi ve
`/health`'e bağlanmış sağlık kontrolleri. Gönderilmiş tipli istemci parçacığı, yeniden deneme + zaman
aşımı + devre kesici önceden yapılandırılmış olarak gelir, böylece bot yazarları dayanıklılığı devralır.

## Kardeş: AI para birimi gücü (`market:read`)

[AI makro para birimi gücü](./currency-strength.md) okuma modeli **aynı** JWT mekanizmasına biner —
bir şema, bir imzalama gizli anahtarı, bir hız sınırlayıcı — yalnızca bir `market:read` kapsamı ekler. O
kapsamla bir API istemcisi kaydedin, tam olarak yukarıdaki gibi bir token ile değiştirin ve çağırın:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// obtain a token via POST /api/calendar/v1/token as above, then:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

`market:read` eksik bir token `403` alır; süresi dolmuş/kurcalanmış bir token `401` alır. Uç noktalar AI
özellik bayrağıyla kapılıdır ve takvim özellik kapısından bağımsız kalmaları için `/api/market/v1`
altında sunulur. Çalıştırma/backtest gönderiminde bir dağıtım `CMIND_API_BASEURL` + kısa ömürlü bir
`market:read` token'ı enjekte edebilir, böylece bir cBot sıfır istemci kaydıyla geri çağırır.
