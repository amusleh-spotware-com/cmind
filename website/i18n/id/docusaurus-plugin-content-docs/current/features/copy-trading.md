---
description: "Copy trading — salin trade dari provider ke follower secara otomatis dengan kontrol penuh."
---

# Copy Trading

Copy trading — salin trade dari provider ke follower secara otomatis dengan kontrol penuh.

## Cara Kerja

### Aliran

```
Provider places trade
       ↓
cMind receives signal
       ↓
CopyEngine processes
       ↓
Order发送到follower akun
```

### two main parties

| Party | Role |
|-------|------|
| **Provider** | Trader whose trades are copied |
| **Follower** | Trader who copies others |

## Membuat Copy Profile

### Via UI

1. Buka **Copy Trading → Marketplace** untuk menemukan provider.
2. Pilih provider → **Copy**.
3. Konfigurasi pengaturan:

```json
{
  "allocation": 1000.00,
  "copyRatio": 1.0,
  "maxSlippage": 5,
  "stopLoss": 10.0,
  "copyStopLoss": true,
  "copyTakeProfit": true
}
```

4. Aktifkan auto-copy atau manual.

### Via API

```http
POST /api/copy/profiles
{
  "masterAccountId": "ACC-123",
  "followerAccountId": "ACC-456",
  "allocation": 1000.00,
  "copyRatio": 1.0
}
```

## Copy Settings

### Allocation

Jumlah dana yang dialokasikan untuk copy:

```
Minimum: $100
Maximum: Unlimited (tergantung regulatory)
```

### Copy Ratio

Rasio untuk menyesuaikan ukuran lot:

| Ratio | Efek |
|-------|------|
| 1.0 | Same lot size as provider |
| 0.5 | Half lot |
| 2.0 | Double lot |

### Stop Loss

Set stop loss untuk seluruh copy portfolio:

```json
{
  "stopLoss": {
    "type": "percentage",
    "value": 10.0
  }
}
```

## Copy Options

### Auto-Copy

Otomatis copy semua trade provider:

```json
{
  "autoCopy": {
    "enabled": true,
    "excludeNews": true,
    "excludeHighImpact": true
  }
}
```

### Manual Copy

Pilih trade mana yang akan dicopy:

- Notification ketika provider menempatkan trade.
- Pilih untuk copy atau skip.
- Setuju per-trade.

## Managing Copy Profiles

### Pause/Resume

```http
POST /api/copy/profiles/{id}/pause
POST /api/copy/profiles/{id}/resume
```

### Modify Settings

```http
PATCH /api/copy/profiles/{id}
{
  "allocation": 2000.00,
  "copyRatio": 0.75
}
```

### Stop Copying

```http
DELETE /api/copy/profiles/{id}
```

Ini akan:
1. Tutup semua posisi terbuka (opsional).
2. Hapus profile.
3. Cabut otorisasi.

## Copy Trading Features

### Slippage Handling

| Slippage | Action |
|----------|--------|
| < 1 pip | Execute immediately |
| 1-5 pips | Execute with warning |
| > 5 pips | Skip or notify |

### Partial Fills

Jika order hanya terisi sebagian:

```json
{
  "partialFill": {
    "action": "scale_lot",
    "fillRatio": 0.7
  }
}
```

### Pending Orders

Copy pending orders (limit, stop):

```json
{
  "copyPendingOrders": true,
  "pendingExpiration": "4h"
}
```

## Fees

### Provider Fees

| Fee Type | Amount |
|----------|--------|
| Performance fee | 5-15% of profit |
| Subscription | $0 - $100/month |

### Follower Costs

| Cost | Amount |
|------|--------|
| Spread | Same as master |
| Commission | Standard rates |
| Copy fee | $0.50 per trade |

## Monitoring

### Dashboard

Halaman **Copy Trading** menampilkan:

- **Active profiles** — semua profile aktif.
- **PnL** — profit/loss dari copy trading.
- **Trades** — history trade yang dicopy.
- **Providers** — performa provider.

### Notifications

| Event | Notification |
|-------|--------------|
| New trade copied | In-app |
| Provider stopped | In-app + Email |
| Stop loss hit | In-app + Email |
| Daily summary | Email |

## Risk Management

### Per-Profile Limits

```json
{
  "limits": {
    "maxPositions": 10,
    "maxLotsPerTrade": 5.0,
    "maxDailyLoss": 5.0
  }
}
```

### Global Limits

```json
{
  "globalLimits": {
    "maxTotalAllocation": 50000.00,
    "maxProviders": 5
  }
}
```

## Troubleshooting

### Trade Not Copied

Kemungkinan penyebab:

1. **Insufficient balance** —follower tidak punya cukup dana.
2. **Max positions reached** —batas posisi tercapai.
3. **Provider stopped** —provider berhenti trading.
4. **Connection lost** — koneksi ke cTrader terputus.

### Large Slippage

Jika slippage terlalu besar:

1. Kurangi copy ratio.
2. Tingkatkan max slippage tolerance.
3. Gunakan limit order daripada market.

## Best Practices

1. **Diversify** — copy 2-3 providers.
2. **Start small** — test dengan allocation kecil.
3. **Monitor closely** — watch first few trades.
4. **Understand strategy** — tahu apa yang Anda copy.
5. **Review regularly** — evaluate provider performance.
