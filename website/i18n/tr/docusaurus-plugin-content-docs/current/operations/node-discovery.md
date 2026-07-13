---
description: "cTrader CLI düğümleri kendi kendini kaydolma + kalp atışıyla kümeye katılır — manuel giriş yok. Consul/Nomad/kubeadm aracılarıyla aynı desen: aracı ana düğüm konumunu…"
---

# Düğüm otomatik keşfi

cTrader CLI düğümleri **kendi kendini kaydolma + kalp atışıyla** kümeye katılır — manuel giriş yok.
Consul/Nomad/kubeadm aracılarıyla aynı desen: aracı ana düğüm konumunu + paylaşılan küme sırrını bilinerek
açılır, sonra kendisini sürekli duyurur.

> Docker Compose ve `kind` Kubernetes kümesinde uçtan uca doğrulanmış: aracılar kendi kendini kaydeder,
> DB'de ulaşılabilir olarak görünür, kalp atışları TTL'yi geçmeyi durdurduğunda otomatik olarak
> ulaşılamaz işaretlenmemiş, devam ettiklerinde çevrimiçi döner.

## Nasıl çalışır

```
CtraderCliNode aracısı                      Ana (Web)
------------------                         ----------
POST /api/nodes/register  ── birleştirme jetonunu ──▶ jetonunu doğrula (sabit zaman)
  { name, baseUrl, mode,                           protokol sürümünü doğrula
    maxInstances, dataDir,                         ad ile CtraderCliNode upsert
    protocolVersion }                              LastHeartbeatAt'ı damgala, IsReachable=true
        ▲                                           └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  her HeartbeatInterval                    NodeHeartbeatMonitor (arka plan):
        └──────────────────────────────────── eğer şimdi - LastHeartbeatAt > HeartbeatTtl
                                                  → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Kayıt == kalp atışı.** Aracı `HeartbeatIntervalSeconds` 'de yeniden POST yapar. İlk çağrı düğümü
  oluşturur (`NodeRegistered` olayı); sonraki çağrılar yaşamlılık yenilemesi. Kesintiden sonra devam
  eden kalp atışı, düğümü ulaşılabilir olarak geri çevirir (`NodeCameOnline`).
- **Yaşamlılık uzlaştırması.** `NodeHeartbeatMonitor` son kalp atışı `HeartbeatTtl` 'yi aşan düğümleri
  ulaşılamaz işaretler. Planlayıcı (`IsActive`/`AcceptsRun`/`AcceptsBacktest` erişilebilirliğe kapılı)
  tekrar rapor verene kadar işleri yerleştirmeyi durdurur.
- **Yetim örnek tahliyesi.** `NodeInstanceReclaimer` (arka plan) ulaşılamaz bir düğümde mahsur kalmış
  herhangi bir terminal olmayan örneği **Başarısız**'a geçiştirir (`FailureReason = "Düğüm ulaşılamaz -
  örnek talep edildi"`, `InstanceFailed` alan olayı → kullanıcı bildirimi), kırılmış/bölünmüş düğüm
  örneği asla "Çalıştırılıyor" için kilitleyebilir. Tahliye yalnızca düğümün son kalp atışı
  `HeartbeatTtl + InstanceReclaimGrace` 'nin ötesinde eski olduğunda açılır, kısa dönem bir kurtarma
  şansı verme. Talep edilen **çalışmalar otomatik olarak yeniden planlanmaz**: bölünen ancak canlı
  düğüm konteyner yürütmeyi durdurabilir ve hiçbir konteyner seviyesi çit yoktur, bu nedenle yeniden
  başlatmak çift yürütme riskine girebilir — kullanıcı talep edilen çalışmayı kasıtlı olarak yeniden
  başlatır. Backtestler kendi kendinden çıkış, bu nedenle talep edilen backtest basitçe yeniden
  çalıştırılır.
- **Kimlik düğüm adıdır.** Ana `NodeName` tarafından upsert yapar, bu nedenle yeniden başlatmada IP/URL
  değişen pod kimlik tutar, yeni `AdvertiseUrl` 'yi yeniden kaydeder.
- **Mod ilk kaydında sabitlenir.** Düğüm modu (`Run`/`Backtest`/`Mixed`) kalıcı tiptir, kalp atışında
  değişemez; farklı modda yeniden kayıt yaşamlılık için onurlandırılır ancak mod değişikliği yoksayılır
  (uyarı olarak günlüğe kaydedilir). Modu değiştirmek için: düğümü silin, yeniden kaydolmasına izin verin.

## Yapılandırma

Ana (Web) — `App:Discovery`:

| Anahtar | Varsayılan | Anlam |
|-----|---------|---------|
| `Enabled` | `false` | Kayıt uç noktası + monitör için ana anahtarı. |
| `JoinToken` | — | Paylaşılan küme sırrı (≥ 32 karakter) aracıları sunmalı. |
| `HeartbeatTtl` | `00:01:30` | Sessiz düğüm ulaşılamaz işaretlenmeden önceki zaman. |
| `InstanceReclaimGrace` | `00:01:00` | Ulaşılamaz bir düğümde mahsur örnek talep edilmeden (başarısız) önce `HeartbeatTtl` 'nin ötesine ekstra marj. |
| `MonitorInterval` | `00:00:30` | Monitor ve örnek tahliyecinin ne sıklıkta süpürür. |
| `HeartbeatInterval` | `00:00:30` | Aracılara önerilen tempo olarak döndürülen değer. |

Aracı (CtraderCliNode) — `NodeAgent`:

| Anahtar | Anlam |
|-----|---------|
| `MainUrl` | Ana düğüm taban URL'si. Boş = manuel kayıt modu (döngü-noop). |
| `AdvertiseUrl` | Ana kullanım **bu** aracıya ulaşmak için URL. |
| `NodeName` | Benzersiz ad; boş ise makine adını varsayılan yapar. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Planlayıcı tarafından onurlandırılan kapasite ipucu. |
| `HeartbeatIntervalSeconds` | Yeniden kayıt temposu. |
| `JwtSecret` | Ana `JoinToken` 'a eşit olmalı — hem kayıt taşıyıcı hem de gönderme JWT imzalama anahtarı. |

## Güvenlik modeli (v1)

Otomatik kaydedilen düğümler **bir küme sırrını** paylaşır (`JoinToken` == her aracının `JwtSecret`).
Ana her gönderme isteğini bu sırla 5 dakikalık HS256 JWT olarak imzalar; aracı doğrular. Gereksinimler:

- `JoinToken` tutun ≥ 32 karakter ve döndürün (ana `App:Discovery:JoinToken` ve her aracının
  `NodeAgent:JwtSecret` 'u birlikte güncelleyin).
- Üretimde ana ve aracılarının önünde TLS sonlandırın (ters proxy / ingress).
- Aracı yine de sadece `AllowedImagePrefix` ile eşleşen görüntüleri çalıştırır.

**Sertleştirme takip (v1 değil):** kayıtta benzersiz per-düğüm sırrı yayın (kubeadm-style bootstrap →
per-düğüm kimlik bilgisi) böylece tek uzlaştırılmış aracı eşleri için gönderme jetonları
sahteleştirilemez. Kayıt akışı zaten yanıt gövdesini döndürür — basılmış per-düğüm sırrını geri vermek
için doğal yer.

## Manuel düğümler hala çalışır

`POST /api/nodes` (admin UI) sabitlenmiş düğümleri kendi per-düğüm sırrı ile kaydetmeye devam eder.
Keşif katkıdır.

Beyaz etiket dağıtımı **manuel kontrolleri gizleyebilir** (veya tüm Düğümler yüzeyini) ve tamamen
otomatik keşif üzerine güvenebilir: `App:Branding:NodesUi=Monitor` manuel ekle/sil'i bırakır, `Hidden`
nav, sayfa ve manuel API'yi kaldırır ve `App:Branding:RestrictNodesToOwner` yüzeyi yalnızca sahibi
ile topraklandırır. Buradaki kendi kendini kaydolma ve kalp atışı uç noktası her modda etkilenmez.
Bkz. [Beyaz etiket → Düğümler UI görünürlüğü](../features/white-label.md#nodes-ui-visibility).
