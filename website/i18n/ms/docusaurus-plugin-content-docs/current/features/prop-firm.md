---
description: "Firma prop runcit (gaya FTMO) menjual akaun penilaian: pedagang harus mencapai sasaran keuntungan sambil kekal dalam had risiko (harian rugi max, max undur/trailing, konsistensi, had masa) sebelum dibiayai."
---

# Simulasi cabaran firma prop

Firma prop runcit (gaya FTMO) menjual **akaun penilaian**: pedagang harus mencapai sasaran keuntungan sambil
kekal dalam had risiko (harian rugi max, max undur/trailing, konsistensi, had masa) sebelum
dibiayai. cMind membolehkan pengguna cipta **cabangan challenge tersuai mana-mana bentuk industri**, ikat kepada
`TradingAccount`, **jalankan seperti operasi salinan perdagangan** — bermula/dihentikan, dihos pada nod,
Jejaknya **live melalui cTrader Open API**. Agregat menilai setiap peraturan secara deterministik; pada
lulus atau pelanggaran, berakhir challenge, tandakannya, maklumkan pengguna.

## Domain (konteks sempadan: PropFirm)

`PropFirmChallenge` = akar agregat (modul `Core.PropFirm`), merujuk `TradingAccount` nó oleh
ID kukuh sahaja (tiada FK agregat silang). Memiliki penilaian peraturan, mesin status/fasa, lesen nod.

### Objek nilai & set peraturan

- **`Money`** (non-negatif), **`MoneyAmount`** (ditandatangani), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — bacaan diberi kepada agregat.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — fakta bukan ekuiti.
- **`DailyLossLimit`** `(percent, basis)` — basis `Equity` (intraday, termasuk P&L terapung) atau `Balance`
  (realisasi sahaja).
- **`DrawdownLimit`** — `Static` (dari baki permulaan), `TrailingPercent` (dari puncak ekuiti), atau
  `TrailingThresholdDollar` (trail puncak ekuiti oleh jumlah dolar tetap, kemudian **kunci pada baki
  permulaan** sebaik ekuiti mencapai ambang — gaya futures).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — blok lulus semasa satu hari mendominasi keuntungan keseluruhan.
- **`ChallengeRules`** membawa yang di atas ditambah `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Matematik peraturan tinggal pada objek nilai
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); agregat
  mengorkestrator.

### Jenis cabangan & templat

`ChallengeTemplates.For(kind)` membina pratetap sah untuk `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding`, atau `Custom` (kawalan penuh). UI pra-isikan templat; pengguna boleh laras mana-mana medan.

### Fasa & status

- **Fasa:** `Evaluation → Verification → Funded` (langkah tunggal melangkau Verification).
- **Status:** `Active`, `Passed`, `Failed`, tambah `Stopped` (jejak dihentikan) — `Create` bermula
  cabangan `Active`; `Stop()`/`Resume()` togolkannya `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Penilaian peraturan

- **`RecordEquity(EquitySnapshot, now)`** — gulung hari perdagangan pada sempadan hari (merekam
  keuntungan hari sebelumnya untuk peraturan konsistensi), mengemas kini puncak/harian, kemudian **gagal pada pelanggaran pertama**
  (harian rugi → undur → had masa, dalam urutan) atau memajuan fasa apabila sasaran keuntungan,
  hari perdagangan minimum, keperluan konsistensi semua dipenuhi. Snapsot tidak berturut-turut dan rekod pada
  cabangan terminal membaling `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — menilai peraturan kelakuan (max posisi terbuka, pegangan周末,
  perdagangan berita), cop aktiviti untuk peraturan tidak aktif.
- **`PropFirmDrawdownWarning`** lembut dibakar sekali apabila penggunaan ekuiti merentasi ambang yang boleh dikonfigurasi.

Peristiwa domain: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Jejak langsung (Pelaksanaan) — dihos nod, penyembuhan diri

Jejak mencerminkan timbunan hos salinan perdagangan dengan tepat; penjejak prop = **sepupu baca sahaja** enjin salinan.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` pada setiap nod, gerbang pada
  `App:PropFirm:Enabled`. Setiap kitaran **menuntut** cabangan aktif pada lesen penyembuhan diri
  (`AssignedNode` + `LeaseExpiresAt`; lesen luput nod mati dikembalikan sebaik lesen tamat —
 claim atomik `ExecuteUpdate` yang sama seperti salinan perdagangan, jadi dua nod tidak pernah dwi-jejak), memperbaharui lesen,
  menolak token yang diputar di tempat, menghentikan hos yang cabangannya meninggalkan `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — satu setiap cabangan. Membuka `IOpenApiTradingSession`
  untuk akaun dan, pada `App:PropFirm:EquityPollInterval`, mengira semula ekuiti langsung, memberi makan kepada
  agregat. Menukar akses token di tempat pada putaran (tiada penurunan sesi). Keluar apabila cabangan tidak lagi `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — matematik ekuiti yang setia kepada cTrader.
  Ekuiti **tidak** dihantar oleh Open API, jadi diperoleh: `equity = balance + Σ(P&L tidak direalisasi)`,
  di mana P&L setiap posisi ialah `perbezaan harga × unit × kadar sebut harga→deposit + swap + komisen`
  (`unit = volum wire / 100`; panjang dinilai pada bid, pendek pada ask). Baki dari
  `ProtoOATrader`; posisi (harga entry, swap, komisen) dari sepadan; bid/ask langsung dari langganan spot. Murni dan terpencil — hotspot penukaran mata wang unit-diuji pada dirinya sendiri.

## Makluman

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) melanggan acara domain lulus/pelanggaran/amaran
(daftar sebagai `IDomainEventHandler<>`, dihantar selepas `SaveChanges` yang berjaya), memberitahu pengguna
melalui makluman berstruktur/jejak audit (`LogMessages`). UI langsung mencerminkan perubahan status yang sama. Ini
= reaksi lintas-konteks — tidak pernah bermutasi agregat cabangan.

## API (`/api/prop-firm`, ciri `PropFirm`, peran User+)

| Kaedah | Laluan | Tujuan |
|--------|-------|--------|
| GET | `/challenges` | senarai cabangan pengguna (jenis, fasa, status, ekuiti langsung, lesen) |
| GET | `/challenges/{id}` | satu cabangan |
| GET | `/templates` | pratetap industri untuk dialog cipta |
| POST | `/challenges` | cipta dari templat **atau** set peraturan khusus sepenuhnya |
| POST | `/challenges/{id}/start` | sambung jejak (Stopped → Active) |
| POST | `/challenges/{id}/stop` | hentikan jejak (Active → Stopped, lepas lesen) |
| POST | `/challenges/{id}/equity` | rekod snapshot ekuiti → menilai semula (laluan manual/tiada-suapan langsung) |
| DELETE | `/challenges/{id}` | padam lembut (dihalang semasa Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` dedahkan senarai/cipta(dari templat)/rekod-equity/mula/berhenti, gerbang pada
ciri `PropFirm`.

UI: `/prop-firm` (nav *Prop Firm*, gerbang oleh tanda `PropFirm`) senaraikan cabangan dengan tindakan baris **Mula/Berhenti/Padam** (Mula apabila Stopped, Berhenti apabila Active, Padam dilumpuhkan semasa Active), cipta nó melalui `NewPropFirmChallengeDialog` (pemilih templat + editor peraturan penuh). Semua cipta/edit melalui dialog MudBlazor.

## Suapan ekuiti langsung — diselesaikan

Lobang "tiada suapan P&L akaun langsung" sebelum ini ditutup: apabila `App:PropFirm:Enabled` ditetapkan, nod
menjejaki akaun live melalui Open API, memberi makan ekuiti secara automatik. Tanpa nó (lalai), domain dan
laluan `POST …/equity` manual berfungsi tidak berubah — tiada bukti cTrader diperlukan untuk bina/uji/E2E.

## Ujian

- **Unit** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (pemajuan fasa, hari-min, undur statik/trailing,
  harian rugi, penjaga terminal/di luar-urutan); `PropFirmChallengeRulesTests` (asas undur harian vs ekuiti, trailing-threshold-dollar trail+kunci, blok/benarkan konsistensi, had masa, tidak aktif,
  max-pendedahan,周末, berita, stop/resume, sempadan lesen, lulus melepaskan lesen, amaran undur); `PropFirmValueObjectTests` (julat VO + matematik peraturan-VO); `PropFirmEquityCalculatorTests` (P&L panjang/pendek,
  swap/komisen, penukaran sebut harga→deposit, harga yang hilang); `PropFirmTrackingHostTests` (ekuiti langsung memandu lulus/gagal terhadap sesi palsu dilanjutkan); `PropFirmAlertNotifierTests`. Masa nyata /
  `FakeTimeProvider` — tiada baca jam dinding.
- **Integrasi** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (pusingan-pusingan + rekod-equity +
  padam lembut, peraturan diperkaya + lesen pusingan-pusingan) dan `PropFirmTrackingLeaseTests` (tuntutan, lesen dipertikaikan,
  pengambilan semula selepas laps merentasi dua identiti nod) pada Postgres sebenar.
- **E2E** — `E2ETests/PropFirmTests.cs`: cipta + rekod-equity kepada `Passed`; laluan berhenti→mula→pelanggaran;
  laluan templat.
- **Tekanan / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: strim ekuiti/aktiviti rawak bersumber (gulungan hari, lonjakan, kejatuhan, snapshot pendua + di luar pesanan, pendedahan周末/berita) merentasi banyak cabangan peraturan bercampur, menegaskan keadaan terminal tepat sekali, puncak-terikat-kini invarian,
  kegagalan yang reasoned. Masa nyata /
  `FakeTimeProvider` — tiada baca jam dinding.

## Konfigurasi (`App:PropFirm`)

`Enabled` (lalai off), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
