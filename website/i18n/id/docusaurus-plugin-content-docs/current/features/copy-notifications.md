---
description: "Notifikasi copy-trading — alert real-time untuk event penting seperti posisi dibuka/ditutup, target tercapai, dan pelanggaran aturan."
---

# Copy Notifications

Notifikasi copy-trading — alert real-time untuk event penting seperti posisi dibuka/ditutup, target tercapai, dan pelanggaran aturan.

## Channel

| Channel | Ketersediaan |
|---------|-------------|
| In-app | Selalu aktif |
| Email | Konfigurasi per user |
| Webhook | Enterprise only |

## Event Types

### Position Events

| Event | Trigger |
|-------|---------|
| `PositionOpened` | Posisi baru dibuka di akun master |
| `PositionModified` | Stop loss / take profit diubah |
| `PositionClosed` | Posisi ditutup (manual atau SL/TP) |
| `PendingOrderPlaced` | Pending order dipasang |
| `PendingOrderFilled` | Pending order terisi |
| `PendingOrderCancelled` | Pending order dibatalkan |

### Account Events

| Event | Trigger |
|-------|---------|
| `CopyStarted` | Copy-trading diaktifkan |
| `CopyStopped` | Copy-trading dinonaktifkan |
| `MasterDisconnected` | Master tidak ada aktivitas > 5 menit |
| `MasterReconnected` | Master aktif kembali |
| `AccountLinked` | Akun baru ditautkan |
| `AccountUnlinked` | Akun dilepas |

### Risk Events

| Event | Trigger |
|-------|---------|
| `DailyLossApproaching` | Mendekati batas harian |
| `DailyLossExceeded` | Melebihi batas harian |
| `TotalLossApproaching` | Mendekati batas total |
| `TotalLossExceeded` | Melebihi batas total |
| `DrawdownAlert` | Drawdown > 50% dari batas |
| `PositionSizeExceeded` | Ukuran posisi > batas |

### Performance Events

| Event | Trigger |
|-------|---------|
| `ProfitTargetHit` | Target profit harian tercapai |
| `WeeklyProfitSent` | Laporan mingguan |
| `MonthlyStatement` | Laporan bulanan |
| `PerformanceMilestone` | Milestone (mis. +10% profit) |

## Konfigurasi

### Per-Event

```json
{
  "notifications": {
    "PositionOpened": {
      "inApp": true,
      "email": false,
      "webhook": true
    },
    "DailyLossApproaching": {
      "inApp": true,
      "email": true,
      "webhook": true
    }
  }
}
```

### Global Settings

```json
{
  "notificationSettings": {
    "digestMode": "realtime",    // realtime atau digest
    "digestInterval": "1h",      // Jika digest mode
    "quietHours": {
      "enabled": true,
      "start": "22:00",
      "end": "08:00"
    }
  }
}
```

## Alert Fatigue Reduction

### Aggregation

Notifikasi serupa diagregasi:

- "5 posisi dibuka" bukan 5 notifikasi individual.
- "EURUSD naik 100 pips" untuk semua trade di EURUSD.

### Throttling

Rate limiting pada notifikasi:

- Maks 10 notifikasi per menit per akun.
- Burst allowed sampai 20, lalu throttled.

### Smart Routing

Notifikasi优先放到正确的地方:

- Urgent (loss limits) → SMS + call + email.
- Info (trade opened) → in-app saja.
- Summary (daily report) → email.

## Webhook Format

```json
{
  "event": "PositionClosed",
  "timestamp": "2024-01-15T14:30:00Z",
  "accountId": "ACC-456",
  "masterAccountId": "ACC-123",
  "copyProfileId": "PROF-789",
  "symbol": "EURUSD",
  "direction": "Buy",
  "lots": 0.5,
  "pnl": 125.50,
  "fees": 2.50,
  "netPnL": 123.00
}
```
