---
description: "Transparansi copy-trading — fee yang jelas, breakdown biaya, disclosure, dan audit trail."
---

# Copy Execution Transparency

Transparansi copy-trading — fee yang jelas, breakdown biaya, disclosure, dan audit trail.

## Ringkasan Eksekutif

Setiap operasi copy-trading dilacak dengan full audit trail. Follower dapat melihat:

- **Sumber trade** — akun master mana yang ditiru.
- **Waktu eksekusi** — timestamps akurat untuk setiap tindakan.
- **Fee breakdown** — semua biaya dijelaskan secara transparan.
- **Slippage aktual** — perbedaan antara harga yang diharapkan dan harga eksekusi.

## Audit Trail

### Events

Setiap event dalam copy-trading dicatat:

| Event | Deskripsi |
|-------|-----------|
| `CopyStarted` | Follower mulai copy master |
| `CopyStopped` | Follower berhenti copy |
| `OrderPlaced` | Order ditempatkan di akun follower |
| `OrderFilled` | Order terisi (dengan harga aktual) |
| `OrderCancelled` | Order dibatalkan |
| `PositionClosed` | posisi ditutup |
| `FeeCharged` | Biaya admin/dperformance fee |

### Data yang Dicatat

```json
{
  "event": "OrderFilled",
  "timestamp": "2024-01-15T10:30:00Z",
  "masterAccountId": "ACC-123",
  "followerAccountId": "ACC-456",
  "symbol": "EURUSD",
  "direction": "Buy",
  "lots": 0.5,
  "entryPrice": 1.0850,
  "expectedPrice": 1.0848,
  "slippage": 0.2,
  "copyFee": 0.50,
  "performanceFee": 1.25
}
```

## Fee Structure

### Biaya Tetap

| Tipe | Jumlah | Deskripsi |
|------|--------|-----------|
| Copy setup fee | $0 | Tidak ada biaya untuk memulai |
| Monthly fee | $0 | Tidak ada biaya langganan |
| Per-trade fee | $0.50 | Biaya per trade yang diisi |

### Performance Fee

| Tier | Volume Bulanan | Performance Fee |
|------|----------------|----------------|
| Starter | < $10,000 | 10% |
| Pro | $10,000 - $50,000 | 8% |
| Elite | > $50,000 | 5% |

Fee dihitung dari profit bersih高于 watermarked.

## Transparansi Harga

### Slippage Reporting

Slippage aktual dilacak untuk setiap order:

- **Expected price** — harga yang diharapkan (harga master).
- **Actual price** — harga eksekusi aktual.
- **Slippage in pips** — perbedaan.
- **Slippage vs spread** — dibandingkan dengan spread saat itu.

### Execution Quality

Dashboard menampilkan quality metrics:

- **Average slippage** — slippage rata-rata.
- **Fill rate** — persentase order yang terisi.
- **Rejection rate** — persentase order yang ditolak.
- **Average execution time** — waktu dari signal ke eksekusi.

## Laporan Follower

Follower menerima laporan berkala yang mencakup:

- **Summary** — total profit/loss.
- **Fees paid** — semua biaya.
- **Trades copied** — daftar trade.
- **Performance attribution** — kontribusi dari masing-masing master.
- **Risk metrics** — exposure, drawdown, dll.

## Dispute Resolution

Jika follower merasa ada ketidakakuratan:

1. **Audit request** — minta audit trail lengkap.
2. **Evidence review** — tim review semua data.
3. **Resolution** — penyesuaian jika diperlukan.
