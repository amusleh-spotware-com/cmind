---
description: "cTrader CLI düğümleri kümeye kendi-kaydı + kalp atışıyla katılır — manuel giriş yok. Consul/Nomad/kubeadm ajanlarıyla aynı desen: ajan, ana düğüm konumunu bilerek açılır…"
---

# Düğüm otomatik keşfi

cTrader CLI düğümleri kümeye **kendi-kaydı + kalp atışıyla** katılır — manuel giriş yok. Consul/Nomad/kubeadm
ajanlarıyla aynı desen: ajan, ana düğüm konumunu + paylaşılan küme sırrını bilerek açılır, ardından sürekli
kendini duyurur.

> Docker Compose ve `kind` Kubernetes kümesinde uçtan uca doğrulandı: ajanlar kendini kaydeder, DB'de
> erişilebilir görünür, kalp atışları TTL'yi geçince otomatik erişilemez işaretlenir, devam ettiğinde çevrimiçi döner.

## Nasıl çalışır

```
CtraderCliNode ajanı                         Ana (Web)
------------------                         ----------
POST /api/nodes/register  ── katılım belirteci ──▶ belirteci doğrula (sabit-zaman)
  { name, baseUrl, mode,                    protokol sürümünü doğrula
    maxInstances, dataDir,                   CtraderCliNode'u ada göre upsert et
    protocolVersion }                        LastHeartbeatAt damgala, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  her HeartbeatInterval              NodeHeartbeatMonitor (arka plan):
        └──────────────────────────────────── if now - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Kayıt == kalp atışı.** Ajan `HeartbeatIntervalSeconds`'te yeniden POST eder. İlk çağrı düğümü oluşturur
  (`NodeRegistered` olayı); sonraki çağrılar canlılığı tazeler. Kesintiden sonra devam eden kalp atışı düğümü
  tekrar erişilebilir yapar (`NodeCameOnline`).
- **Canlılık uzlaşması.** `NodeHeartbeatMonitor`, son kalp atışı `HeartbeatTtl`'yi aşan düğümleri erişilemez
  işaretler. Zamanlayıcı (`IsActive`/`AcceptsRun`/`AcceptsBacktest` erişilebilirliğe kapılı) tekrar rapor
  edene kadar iş yerleştirmeyi durdurur.
- **Yetim-örnek geri alma.** `NodeInstanceReclaimer` (arka plan), erişilemez bir düğümde mahsur kalan herhangi
  bir terminal-olmayan örneği **Failed**'a geçirir (`FailureReason = "Node unreachable - instance reclaimed"`,
  `InstanceFailed` alan olayı → kullanıcı bildirimi), böylece çöken/bölünmüş bir düğüm bir örneği asla sonsuza
  dek "Running" bırakamaz. Geri alma yalnızca düğümün son kalp atışı `HeartbeatTtl + InstanceReclaimGrace`'in
  ötesinde bayatladığında tetiklenir, kısa bir kesintiye önce kurtulma şansı verir. Geri alınan **çalıştırmalar
  otomatik yeniden zamanlanmaz**: bölünmüş-ama-canlı bir düğüm hâlâ konteyneri yürütüyor olabilir ve
  konteyner-düzeyinde çitleme yok, bu yüzden yeniden başlatmak çift yürütme riski taşır — kullanıcı geri alınan
  bir çalıştırmayı bilinçli olarak yeniden başlatır. Backtest'ler kendi-çıkış yapar, bu yüzden geri alınan bir
  backtest yalnızca yeniden çalıştırılır.
- **Kimlik, düğüm adıdır.** Ana, `NodeName`'e göre upsert eder, böylece yeniden başlatmada IP/URL'si değişen
  pod kimliğini korur, yeni `AdvertiseUrl`'yi yeniden kaydeder.
- **Mod ilk kayıtta sabittir.** Düğüm modu (`Run`/`Backtest`/`Mixed`) kalıcı türdür, kalp atışında değişemez;
  farklı modla yeniden kayıt canlılık için onurlandırılır ancak mod değişikliği yoksayılır (uyarı olarak
  günlüğe kaydedilir). Modu değiştirmek için: düğümü sil, yeniden kaydolmasına izin ver.

## Yapılandırma

Ana (Web) — `App:Discovery`:

| Anahtar | Varsayılan | Anlamı |
|-----|---------|---------|
| `Enabled` | `false` | Kayıt uç noktası + monitör için ana anahtar. |
| `JoinToken` | — | Ajanların sunması gereken paylaşılan küme sırrı (≥ 32 karakter). |
| `HeartbeatTtl` | `00:01:30` | Sessiz düğüm erişilemez işaretlenmeden önce süre. |
| `InstanceReclaimGrace` | `00:01:00` | Erişilemez bir düğümdeki mahsur örnek geri alınmadan (başarısız) önce `HeartbeatTtl`'nin ötesinde ekstra pay. |
| `MonitorInterval` | `00:00:30` | Monitörün ve örnek-geri-alıcının ne sıklıkta taradığı. |
| `HeartbeatInterval` | `00:00:30` | Önerilen tempo olarak ajanlara döndürülen değer. |

Ajan (CtraderCliNode) — `NodeAgent`:

| Anahtar | Anlamı |
|-----|---------|
| `MainUrl` | Ana düğümün temel URL'si. Boş = manuel kayıt modu (döngü no-op). |
| `AdvertiseUrl` | Ananın **bu** ajana ulaşmak için kullandığı URL. |
| `NodeName` | Benzersiz ad; boşsa makine adına varsayılır. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Zamanlayıcı tarafından onurlandırılan kapasite ipucu. |
| `HeartbeatIntervalSeconds` | Yeniden kayıt temposu. |
| `JwtSecret` | Ananın `JoinToken`'ına eşit olmalı — hem kayıt taşıyıcısı hem de dağıtım JWT imzalama anahtarı. |

## Güvenlik modeli (v1)

Otomatik kaydolan düğümler **bir küme sırrını** paylaşır (`JoinToken` == her ajanın `JwtSecret`'i). Ana, her
dağıtım isteğini o sırla 5-dakikalık HS256 JWT olarak imzalar; ajan doğrular. Gereksinimler:

- `JoinToken`'ı ≥ 32 karakter tutun ve döndürün (ananın `App:Discovery:JoinToken`'ını ve her ajanın
  `NodeAgent:JwtSecret`'ini birlikte güncelleyin).
- Üretimde ana ve ajanların önünde TLS sonlandırın (ters vekil / ingress).
- Ajan yine yalnızca `AllowedImagePrefix`'e uyan imajları çalıştırır.

**Sağlamlaştırma takibi (v1 değil):** kayıt sırasında benzersiz düğüm-başına sır ver (kubeadm tarzı önyükleme
→ düğüm-başına kimlik bilgisi), böylece tek bir ele geçirilmiş ajan, akranlar için dağıtım belirteçleri
uyduramaz. Kayıt akışı zaten yanıt gövdesi döndürür — basılan düğüm-başına sırrı geri vermek için doğal yer.

## Manuel düğümler hâlâ çalışır

`POST /api/nodes` (yönetici UI), sabitlenmiş düğümleri kendi düğüm-başına sırlarıyla kaydetmeye devam eder.
Keşif ek niteliktedir.

Bir white-label dağıtımı, **manuel denetimleri** (veya tüm Nodes yüzeyini) **gizleyebilir** ve tamamen
otomatik-keşfe dayanabilir: `App:Branding:NodesUi=Monitor` manuel ekle/sil'i düşürür, `Hidden` nav'ı, sayfayı
ve manuel API'yi kaldırır ve `App:Branding:RestrictNodesToOwner` yüzeyi yalnızca-sahibe indirir. Buradaki
kendi-kaydı + kalp atışı uç noktası her modda etkilenmez. Bkz.
[White-label → Nodes UI görünürlüğü](../features/white-label.md#nodes-ui-visibility).
