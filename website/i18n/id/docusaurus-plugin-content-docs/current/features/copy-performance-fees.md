---
description: "Performance fee copy-trading — model fee, kalkulasi, dan payout."
---

# Copy Performance Fees

Performance fee copy-trading — model fee, kalkulasi, dan payout.

## Model Fee

### Standard Model

Fee dihitung sebagai persentase dari profit bersih:

```
Performance Fee = Net Profit × Fee Rate
```

Dimana:
- **Net Profit** = Gross Profit - Commissions - Overnight Fees - Slippage Cost
- **Fee Rate** = sesuai tier follower (5-15%)

### High-Water Mark

Profit dihitung dari high-water mark (HWM):

```
HWM = Highest peak of account equity

Profit since HWM = Current Equity - HWM
Performance Fee = Profit since HWM × Fee Rate
```

Ini memastikan fee hanya dibayar untuk profit baru, bukan profit yang sudah ada.

## Fee Tiers

| Tier | Monthly Volume | Fee Rate | Minimum |
|------|---------------|-----------|---------|
| Starter | < $10,000 | 15% | $0 |
| Growth | $10,000 - $50,000 | 12% | $100 |
| Pro | $50,000 - $100,000 | 10% | $250 |
| Elite | > $100,000 | 5% | $500 |

Volume dihitung sebagai total volume trading bulan sebelumnya.

## Kalkulasi Contoh

### Contoh 1: Profit dengan HWM

```
Start Balance: $10,000
HWM: $11,000
End Balance: $11,500

Profit = $500
Fee = $500 × 10% = $50
```

### Contoh 2: Loss setelah HWM

```
HWM: $11,000
Current Balance: $10,500

Loss = $0 (tidak ada fee karena di bawah HWM)
```

### Contoh 3: Volume-Based Tier Upgrade

```
Month 1 Volume: $8,000 → Starter (15%)
Month 2 Volume: $12,000 → Growth (12%)
Month 3 Volume: $55,000 → Pro (10%)
```

## Fee Schedule

### Daily Accrual

Fee accrues setiap hari berdasarkan P&L harian:

```csharp
dailyAccrual = dailyNetProfit × feeRate;
accruedFees += dailyAccrual;
```

### Monthly Payout

Fee dihitung ulang setiap bulan:

1. **Month-end snapshot** — equity recorded.
2. **HWM adjustment** — jika equity > HWM, HWM di-update.
3. **Fee calculation** — profit dihitung dari HWM.
4. **Payout** — fee ditransfer ke master.

```csharp
if (endOfMonth)
{
    var monthEndEquity = GetCurrentEquity();
    if (monthEndEquity > hwm)
    {
        var profit = monthEndEquity - hwm;
        var fee = profit × feeRate;
        TransferToMaster(fee);
        hwm = monthEndEquity;
    }
}
```

## Reporting

### Master Dashboard

Master melihat:

- **Accrued fees** — fee yang belum dibayar.
- **Paid fees** — fee yang sudah ditransfer.
- **HWM history** —记录 perubahan HWM.
- **Volume breakdown** — volume per follower.

### Follower Dashboard

Follower melihat:

- **Fee summary** — total fee yang dibayar.
- **Fee detail** — per-trade breakdown.
- **HWM status** — HWM saat ini dan posisi.
- **Projected fees** — estimasi fee bulan ini.

## Kontrol Follower

### Opt-Out

Follower dapat opt-out dari performance fee:

```json
{
  "feeSettings": {
    "performanceFeeEnabled": false,
    "preferredFeeModel": "fixed",    // Atau "none"
    "fixedFeePerTrade": 1.00
  }
}
```

### Fee Cap

Follower dapat mengatur cap pada fee:

```json
{
  "feeSettings": {
    "maxMonthlyFee": 100.00,
    "maxFeeRate": 10.0
  }
}
```
