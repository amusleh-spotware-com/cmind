---
description: "cMind minimum operatör çabasıyla ölçek genişletir. İki durum bilgili iş yükü — çalıştırma/backtest yürütme, copy-trading — her ikisi de veritabanını koordinasyon noktası olarak kullanır, böylece…"
---

# Yatay ölçekleme

cMind minimum operatör çabasıyla ölçek genişletir. İki durum bilgili iş yükü — çalıştırma/backtest
yürütme, copy-trading — her ikisi de veritabanını koordinasyon noktası olarak kullanır, böylece
replika eklemek harici bir koordinatör gerektirmez (ZooKeeper yok, lider seçimi yok).

## Copy-trading (kendi kendini iyileştiren kiralama)

Her node `CopyEngineSupervisor` çalıştırır (`App:Copy:Enabled` üzerinde geçitli). Her uzlaştırma
döngüsünde, supervisor:

1. Atanmamış *veya* kiralaması süresi dolmuş her çalışan profili tek bir atomik `UPDATE` ile **talep eder** —
   iki yarışan supervisor asla aynı profili birlikte talep etmez, böylece profil tam olarak bir node
   tarafından kopyalanır (çift emir yok).
2. Barındırdığı profillerdeki kiralamayı **yeniler**.
3. Atanan profilleri barındırır, erişim-token rotasyonlarını çalışan host'a yerinde iter (olay-akışı
   düşüşü olmadan).

Node çökmesi → yenilemeyi durdurur; `App:Copy:LeaseTtl` geçtikten sonra, hayatta kalan herhangi bir node
profillerini bir sonraki döngüde geri alır, ticaretleri çoğaltmadan uzlaştırmadan durumu yeniden oluşturur.
**Ölçek genişletme** = replika ekle; atanmamış/boş profiller otomatik olarak alınır.

**Zarif ölçek içe / yuvarlanan güncelleme (S1)** = `SIGTERM`'de, `CopyEngineSupervisor.StopAsync`
**bu node'un kiralamalarını serbest bırakır** (`AssignedNode`/`LeaseExpiresAt` → null), böylece hayatta
kalan bunları *tam bir sonraki* uzlaştırma döngüsünde geri alır — tam `LeaseTtl`'den sonra **değil**.
Yalnızca sert çökme TTL'yi bekler. Copy-agent'ın `terminationGracePeriodSeconds`'ü (varsayılan 30) pod
öldürülmeden önce serbest bırakmanın tamamlanması için zaman verir.

### Düğmeler (`App:Copy`)

| Ayar | Varsayılan | Notlar |
|---------|---------|-------|
| `Enabled` | `false` | Node için copy barındırmayı açar. |
| `ReconcileInterval` | `30s` | Node'un ne sıklıkla talep ettiği/yenilediği/uzlaştırdığı. |
| `LeaseTtl` | `120s` | Sessiz node'un profilleri geri alınmadan önceki süre. Yavaş bir döngünün sahte devir-teslime neden olmaması için birkaç uzlaştırma aralığı tutun. |
| `NodeName` | makine adı | İki supervisor bir host'u paylaştığında ayrı olarak ayarlayın. |

Kubernetes'te copy supervisor'ları Deployment olarak çalışır; istenen paralelliğe `replicas` ayarlayın. Her
pod kararlı bir `NodeName` alır (varsayılan: pod ana bilgisayar adı), böylece kiralamalar pod başına
atfedilir. Veritabanı tek doğruluk kaynağıdır — yapışkan oturum yok, taşınacak pod başına durum yok.

**Dengeli dağıtım (S4):** bir node'un kaç çalışan profil barındırdığını sınırlamak için
`App:Copy:MaxProfilesPerNode` > 0 ayarlayın. Her supervisor daha sonra atomik
`FOR UPDATE SKIP LOCKED` sınırlı talep aracılığıyla **en fazla** kalan boşluğunu talep eder, böylece
profiller ilk supervisor'ın hepsini kapmasi yerine replikalar arasında **dağılır** — tek sıcak pod / SPOF
yok. Skip-locked talep, eşzamanlı talepler altında bile "profil başına tam olarak bir node" garantisini
korur (çift barındırma yok). `0` (varsayılan) = sınırsız (bir node her şeyi barındırır, değişmedi).

**Ölçekte (S7/S8):** her pod uzlaştırmayı `ReconcileInterval`'in %20'sine kadar seğirtir
(`CopyEngineSupervisor.JitteredInterval`), böylece N replika talep/yenile `UPDATE`'i aynı anda ateşlemez
(Postgres gürleyen-sürü). `copyAgent.replicas > 1` olduğunda chart ayrıca replikaları node'lar arasında
dağıtır (`topologySpreadConstraints`) ve `PodDisruptionBudget` (`minAvailable: 1`) ekler, böylece
boşaltma/yükseltme asla copy kapasitesini sıfıra indirmez.

## Çalıştırma/backtest yürütme

`NodeScheduler`, `MaxInstances`'a saygı göstererek en az yüklü uygun node'u seçer; uzak node ajanları
kendi kendine kaydolur ve kalp atışı gönderir (`App:Discovery`), `NodeHeartbeatMonitor`, kalp atışı
`Discovery:HeartbeatTtl`'yi aştığında node'u ulaşılamaz işaretler. Yürütme kapasitesi eklemek için node
ajanları ekleyin; ölü ajan otomatik olarak dolaşılır.

## Ölçek genişletme / yuvarlanan dağıtımda taşımalar

Her Web/MCP replikası başlangıçta EF taşımalarını uygulayan ve owner'ı seed eden `OwnerSeeder` çalıştırır.
N replika aynı anda başladığında bunu güvenli kılmak için, taşıma + seed bir **Postgres oturum danışma
kilidi** (`MigrationLock.RunExclusiveAsync`, anahtar `DatabaseDefaults.MigrationAdvisoryLockKey`) içinde
çalışır: onu edinen ilk replika taşır ve seed eder; geri kalanı kilitte bloke olur, ardından taşımaların
zaten uygulandığını (no-op) ve owner'ın zaten mevcut olduğunu bulur. Ayrı bir taşıma job'u veya lider
seçimi gerekmez. İlk çalıştırma seed'i eklerseniz, tek yazarlı olması için aynı korumalı bloğun **içine**
koyun.

## Node-agent HTTP dayanıklılığı

Ana node her `CtraderCliNode` ajanıyla amaç bölünmüş üç istemci aracılığıyla HTTP üzerinden konuşur,
böylece dalgalı bir node veya ağ asla durumu bozmaz:

- **read** (`status` / `report` / `stats`) — idempotent GET'ler, geçici hatalarda yeniden denenir
  (üstel geri çekilme + seğirme, `NodeAgentHttp.ReadRetryCount`) deneme başına ve toplam zaman aşımlarıyla.
- **write** (`start` / `stop` / `clean`) — idempotent olmayan POST'lar, zaman aşımına uğrar ama **asla
  yeniden denenmez**: yeniden denenen bir `start` bir konteyneri çift başlatabilir.
- **stream** (`logs`) — uzun ömürlü `docker logs -f` akışı sonsuz bir zaman aşımı ve dayanıklılık
  hattı almaz, böylece takip asla kesilmez.

Ulaşılamaz kalan bir node, kalp atışı + [öksüz-instance geri alma](../operations/node-discovery.md)
tarafından ele alınır; HTTP katmanı yalnızca geçici kesintileri yumuşatır.

## Durum bilgisiz katmanlar

Web (Blazor Server + API) ve MCP sunucusu veritabanının arkasında durum bilgisizdir, serbestçe
replike olur. Kimlik doğrulama çerez tabanlıdır; Web'i yük dengeleyicinin arkasında yatay ölçeklendirin.
MCP sunucusu ayrı bir süreç/Deployment'tır, bu yüzden Web'den bağımsız olarak ölçeklenir.

## Veritabanı bağlantı dayanıklılığı

Veritabanını açan her host **yeniden deneyen bir yürütme stratejisi** kullanır, böylece geçici bir
bağlantı kesilmesi veya yönetilen-Postgres failover'ı (RDS / Flexible Server yamalama) kullanıcıya bir
hata olarak yüzeye çıkmak yerine yeniden denenir:

- Web ve MCP, bağlamı Aspire Npgsql bileşeni aracılığıyla `DisableRetry=false` ve açık bir
  `CommandTimeout` (`DatabaseDefaults.CommandTimeoutSeconds`) ile kaydeder.
- CopyAgent (Aspire olmayan), `DatabaseDefaults`'tan aynı
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + komut zaman aşımını uygulayan `UseAppNpgsql`
  aracılığıyla kaydeder.

Tüm yazmalar tek `SaveChanges` / tek `ExecuteUpdate` / tek `ExecuteSql` ifadeleridir, bu yüzden yeniden
deneyen strateji güvenlidir (çok ifadeli işlem manuel `strategy.ExecuteAsync` sarmalaması gerektirmez).
Bir mantıksal işlemde manuel bir işlem veya birden fazla `SaveChanges` eklerseniz, bunu
`db.Database.CreateExecutionStrategy().ExecuteAsync(...)` içine sarın — aksi halde yeniden deneme altında
fırlatır.

## Ölçek genişletme için kontrol listesi

- [ ] Eklenen bağlantı yükü için boyutlandırılmış Postgres (her Web/MCP/node replikası bir havuz açar).
- [ ] Copy profilleri barındırması gereken her node'da `App:Copy:Enabled=true`.
- [ ] Bir arada bulunan her supervisor başına ayrı `App:Copy:NodeName` (K8s: pod başına varsayılan iyi).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Ayrıcalıklı Docker'ın mevcut olduğu yerlere dağıtılan node ajanları (AKS/EKS/EC2/VM, Fargate değil).
- [ ] Çoklu replika Web: `signalr` bağlantı dizesini ayarlayın (Redis backplane) **ve** ingress oturum
      benzeşimini (yapışkan oturumlar) etkinleştirin, böylece bir Blazor devresi canlı bir pod'a yeniden
      bağlanır. Bir bileşen istisnası `MainLayout` `ErrorBoundary` tarafından yakalanır (dostça yeniden
      deneme, devre canlı kalır).
