---
description: "Agent Studio — Otonomi ve Güvenlik Çekirdeği altında (risk zarfı, devre kesici, kill switch, sürümlenmiş sorumluluk reddi onayı) hesaplarınızı hedeflerinize doğru yöneten, karakteri ve arketipi olan persona odaklı, kod gerektirmeyen ticaret ajanları oluşturun."
---

# Agent Studio

Agent Studio, **karaktere sahip bir ticaret ajanı** oluşturmanızı — kod olmadan — ve hesaplarınızın
ölçülebilir hedeflere doğru yönetimini ona vermenizi sağlar. Bir ajan, kişilik odaklı bir cBot gibidir:
bir arketip ve tutum seçersiniz, koruma bariyerlerini ayarlarsınız ve **Otonomi ve Güvenlik Çekirdeği**
altında çalışır.

**AI → Agent Studio**'yu açın (`/agent-studio`).

## Bir ajan oluşturun

**New agent** iletişim kutusu, kod olmadan şunları toplar:

- **Name** ve **archetype** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion veya Breakout/Momentum. Her ön ayar mantıklı bir tempo ve duruş belirler.
- **Attitude** — agresiflik, sabır ve trend takip kaydırıcıları.
- **Managed account(s)** — **ajanı oluşturmak için en az biri gereklidir** (hesabı olmayan bir ajan asla başlayamaz, bu yüzden bir tane seçene kadar *Create* devre dışı kalır). Henüz bir ticaret hesabı bağlamadıysanız, iletişim kutusu bunu söyler ve önce bir tane bağlamanız için sizi yönlendirir.
- **Autonomy level** — **Advisory** (yalnızca önerir) veya **Approval-gated** (yalnızca eylem başına
  onayınızdan sonra hareket eder). **Full Auto** (ticaret başına onay yok) ek olarak arm edilmeden önce
  bir **risk zarfı** ve risk sorumluluk reddinin kabulünü gerektirir.

Persona, ajanın sistem prompt'una **deterministik olarak** derlenir (hiçbir LLM onu yazmaz), böylece aynı
yapılandırma her zaman aynı talimatları üretir — yeniden üretilebilir ve denetlenebilir.

## Kadro

Her ajan bir kontrol odası tablosunda görünür: **hangi ajan, türü, kaç hesap yönettiği, hedefleri,
çalışma durumu ve son eylemi**, **Start / Stop / Kill** kontrolleriyle. Kill switch, çalışan bir ajanı
anında durdurur.

## Güvenlik bir ayar değil, bir alan değişmezidir

Paraya dokunan her şey **Otonomi ve Güvenlik Çekirdeği**'nden geçer:

- **Risk zarfı** — sert emir başına limitler (maksimum günlük zarar, açık maruziyet, pozisyon boyutu,
  kaldıraç, ardışık zararlar, saat başına emir, izin verilen semboller). Her emir gönderilmeden önce buna
  karşı doğrulanır; bir ihlal reddedilir, kısıtlanmaz. Bir ajan Full Auto'ya ulaşabilmeden önce gereklidir.
- **Devre kesici** — bir zarar serisinde, günlük-zarar ihlalinde, bir **sert performans-hedefi ihlalinde**
  veya **AI-sağlayıcı kullanılamamasında** yeni riski deterministik olarak durdurur (çöken veya halüsinasyon
  gören bir model asla yeni pozisyon açmaz).
- **Sürümlenmiş sorumluluk reddi onayı** — Full Auto'yu arm etmek için tek seferlik, sürümlenmiş bir kabul
  gereklidir (yasal olarak gerekli onay, ticaret başına onay değil); sorumluluk reddini yükseltmek yeniden
  onayı zorunlu kılar.
- **Kill switch** — çalışan her ajanda idempotent bir acil durum durdurma.

## Hedefler

Bir ajana **ölçülebilir amaçlar** verin — örn. *maksimum düşüşü %4'ün altında tut*, *kâr faktörü en az
1,5*, *kazanma oranı ≥ %55*. Her hedef **Hard** (bir koruma bariyeri — bir ihlal devre kesiciyi tetikler)
veya **Soft** (yalnızca akıl yürütmeyi yönlendirir) olup, On-track / At-risk / Breached olarak değerlendirilir.

## Karar hattı

Başlatıldığında, bir ajan **7/24 denetlenen bir döngü** (`AgentRuntimeService`) çalıştırır. Her tik'te,
yönetilen her hesap için: **deterministik hesap durumunu** (temel gerçeklik, asla modelin belleği değil)
okur; karar motorundan bir hamle ister; bunu **güvenlik geçidinden** (`AgentDecisionProcessor`) geçirir —
otonomi seviyesi → devre kesici → risk zarfı; yalnızca ekleme yapılan bir **`AgentDecisionRecord`** yazar;
ve geçidin yönlendirdiği şekilde durur veya yürütür. Döngü **hata-yalıtımlıdır** (bir ajanın başarısızlığı
asla başka bir ajana veya host'a dokunmaz) ve **varsayılan olarak güvenlidir**: AI yapılandırılmadıkça
*ve* `App:Ai:AgentRuntimeEnabled` ayarlanmadıkça hareketsizdir ve AI sağlayıcısı kullanılamadığında asla
yeni risk açmaz.

- **Onay geçidi** — bir **Approval-gated** ajanın önerilen emri **Pending** olarak kaydedilir ve owner
  onaylayana kadar hiçbir şey yapmaz (`POST /api/agent-studio/{id}/decisions/{seq}/approve` veya
  `/reject`); **Full Auto** ticaret başına onay olmadan zarftan geçer; **Advisory** yalnızca önerir.
- **Denetim defteri** — her karar yeniden oynatılabilir: akıl yürütme (XAI), alıntıladığı kanıt, geçit
  kararı, emir niyeti ve yürütülüp yürütülmediği, `GET /api/agent-studio/{id}/decisions`'ta.
- **Araştırma masası** — talep üzerine çok-ajanlı bir tartışma: Alpha/Sentiment/Technical/Risk analistleri
  her biri bir görüş verir ve bir Reviewer bir öneri sentezler (`POST /api/agent-studio/{id}/debate`).
- **Bellek** — ajan her kararı hatırlar ve süreklilik için son belleği bir sonraki prompt'una geri çağırır
  (`GET /api/agent-studio/{id}/memory`).

Her kadro satırının **Details**'i, ajanın karar akışını (bekleyen emirlerde Approve/Reject ile),
belleğini ve bir Run-debate sekmesini açar.

## Kapsam

Gönderildi: tam ajan yaşam döngüsü, deterministik güvenlik geçidi, 7/24 çalışma zamanı,
insan-döngüde onay geçidi, denetim defteri ve **canlı cTrader Open API entegrasyonu** — hesap-durumu
deposu (gerçek bakiyeyi, pozisyonları ve lot cinsinden açık maruziyeti okur) ve emir yürütücüsü (gerçek
market emirleri verir, sembol lot boyutu aracılığıyla lot→hacim), her ikisi de yönetilen her hesabın
OAuth kimlik bilgilerini çözer ve bir hesap bağlı olmadığında güvenli bir şekilde bozulur. Modelin emir
üretmesi için **Anthropic API anahtarı gerektirir** (o zamana kadar motor tutar); henüz gelecek olanlar
çok-ajanlı tartışma rolleri ve katmanlı bellek/yansımadır. Çalışma zamanı `App:Ai:AgentRuntimeEnabled`
ayarlanmadıkça kapalıdır, bu yüzden canlı ticaret yalnızca açık, tam onaylı bir katılımda gerçekleşir.

## Yönetilen hesaplar ve düzenleme

Bir ajan oluştururken yönettiği ticaret hesabını/hesaplarını seçersiniz — **oluşturulurken en az biri gereklidir** (*Create* düğmesi bir tane seçilene kadar devre dışı kalır ve create endpoint boş bir seçimi reddeder). Her ajan daha sonra kadro satırındaki kalem simgesinden **düzenlenebilir** (ad, mizaç, otonomi ve yönetilen hesaplar). Yaşam döngüsü kontrolleri (details, edit, start, stop, kill) simge düğmeleridir, her biri eylemin geçerli olmadığı durumlarda devre dışıdır.
