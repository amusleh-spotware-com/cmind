---
description: "Penempatan white-label jarang menghantar setiap keupayaan. Togol ciri membolehkan operador menghidup/matikan ciri produk utama — pada masa penempatan melalui konfigurasi, atau kemudian pada masa jalan, tanpa…"
---

# Togol ciri

Penempatan white-label jarang menghantar setiap keupayaan. Togol ciri membolehkan operador menghidup/matikan ciri produk utama — pada masa penempatan melalui konfigurasi, atau kemudian pada masa jalan, tanpa
penempatan semula. **Semua ciri dilalai aktif**; penempatan hanya menyenaraikan yang berubah.

## Model

- `Core.Features.FeatureFlag` — enum ciri yang boleh digerbang: `Authoring`, `Backtesting`, `Execution`,
  `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`,
  `Compliance`. Admin teras
  permukaan (dashboard, pengguna, nod, auth) tidak pernah boleh digerbang, bukan di sini.
- `Core.Options.FeaturesOptions` — konfigurasi paksi, diikat dari `App:Features`. Setiap sifat
  dilalai `true`.
- `Core.Features.IFeatureGate` — menyelesaikan keadaan **berkesan**: konfigurasi paksi dihamparkan
  dengan utama tunggakan masa jalan opsyen. Dilaksanakan oleh `Infrastructure.Features.FeatureGate`,
  menyimpan tunggakan dengan cepat (`FeatureSettings.OverrideCacheTtl`), membatalkan pada perubahan.

Tunggakan masa jalan disimpan sebagai baris `AppSetting` berpalang `feature.<FeatureFlag>` (nilai `true`/`false`).
Tiada baris = "guna konfigurasi paksi".

## Dua cara melumpuhkan ciri

### 1. Konfigurasi penempatan (paksi)

Tetapkan tanda `false` di bawah `App:Features`. Contoh `appsettings.json`:

```json
{
  "App": {
    "Features": {
      "CopyTrading": false,
      "PropGuard": false
    }
  }
}
```

Atau melalui pembolehubah env (double underscore):

```
App__Features__CopyTrading=false
```

Paksi menggerbang **pendaftaran permulaan** pekerja latar belakang (`Nodes.AddNodes`) dan alat MCP
(`Mcp` server), jadi ciri yang dilumpuhkan dalam konfigurasi tidak pernah memulakan perkhidmaten yang dihoskan atau mendedahkan alat MCPnya.

### 2. Utama tuggakan masa jalan (pemilik)

Pemilik boleh flip mana-mana ciri masa jalan dari **Tetapan → Ciri** (`/settings/features`) atau API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Pemilik)
PUT  /api/features/{flag}      body { "enabled": false }  -> tetapkan tuggakan             (Pemilik)
PUT  /api/features/{flag}      body { "enabled": null  }  -> jelaskan tuggakan (UND)  (Pemilik)
```

Perubahan masa jalan berkuatkuasa serta-merta untuk gerbang masa permintaan (navigasi, API). Pekerja latar belakang dan alat MCP yang digerbang pada permulaan, ambil perubahan masa jalan pada memulakan semula proses seterusnya.

## Apa yang setiap gerbang kuatkuasa

| Lapisan | Mekanisme | Pemasaan |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` penapis endpoint → `404` apabila dilumpuhkan | Masa jalan |
| Navigasi | `NavMenu` menyembunyikan pautan melalui `IFeatureGate.IsEnabled` | Masa jalan |
| Pekerja latar belakang | `AddHostedService` bersyarat dalam `Nodes.AddNodes` | Permulaan (konfigurasi) |
| Alat MCP | `WithTools<>` bersyarat dalam pelayan MCP | Permulaan (konfigurasi) |

Ciri yang dicapai melalui deep link semasa dilumpuhkan memapar halaman kosong — APInya mengembalikan `404`;
nav tidak lagi memaparkannya.

## Peta tanda → permukaan

| Tanda | Kumpulan API | Nav | Pekerja / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | Kumpulan cBots → cBots (param sets setiap dialog cBot) | MCP `CBotTools` |
| Backtesting | (berkongsi `/api/instances`) | Kumpulan cBots → Backtest | — |
| Execution | `/api/instances` | Kumpulan cBots → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Salinan Perdagangan | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | Kumpulan AI → AI; Tetapan → AI (kunci) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | Kumpulan AI → Ejen Portfolio | `PortfolioAgentService` |
| Alerts | `/api/alerts` | Kumpulan AI → Makluman | `AlertEvaluator` |
| PropGuard | `/api/prop` | Kumpulan Prop → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Kumpulan Prop → Cabaran | — |
| Accounts | `/api/ctids` | Akaun Perdagangan | — |
| OpenApi | `/api/openapi` | Tetapan → Open API | — |
| Mcp | `/api/mcp-keys` | Kumpulan AI → Kunci MCP | — |
| Compliance | `/api/compliance` | Tetapan → Undang-undang & Privasi | — |

## Ujian

- **Unit** — `UnitTests/Features/FeaturesOptionsTests.cs`: lalai paksi, pemetaan setiap tanda.
- **Integrasi** — `IntegrationTests/FeatureGateTests.cs`: paksi konfigurasi, tuggakan masa jalan mengatasi
  konfigurasi dan bertekak sebagai `AppSetting`, membersihkan kembali ke paksi (Postgres sebenar).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: melumpuhkan `CopyTrading` pada masa jalan menyembunyikan pautan navnya dan
  `404`s `/api/copy`, membolehkan semula memulihkan kedua-duanya.
