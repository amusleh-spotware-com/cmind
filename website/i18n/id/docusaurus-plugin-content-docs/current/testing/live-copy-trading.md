---
description: "Live copy-trading testing — pengujian end-to-end dengan akun live."
---

# Live Copy Trading Testing

Live copy-trading testing — pengujian end-to-end dengan akun live.

## Prerequisites

### Akun yang Dibutuhkan

| Akun | Tujuan | Status |
|------|--------|--------|
| Master account | Generate signals | Live |
| Follower account | Receive signals | Live |
| Open API credentials | Access cTrader API | Active |

### Setup

1. Dapatkan OAuth token untuk kedua akun.
2. Pastikan akun master memiliki history trading.
3. Konfigurasi sandbox environment:

```json
{
  "Environment": "LiveTesting",
  "MasterAccount": "ACC-MASTER-123",
  "FollowerAccount": "ACC-FOLLOWER-456"
}
```

## Test Cases

### TC1: Basic Copy

```
Steps:
1. Create copy profile (master → follower)
2. Place trade on master account
3. Verify trade copied to follower

Expected:
- Order appears on follower account
- Same symbol, direction, lots
- Execution within 5 seconds
```

### TC2: Stop Loss / Take Profit Copy

```
Steps:
1. Create profile with SL/TP enabled
2. Place trade with SL/TP on master
3. Verify SL/TP on follower

Expected:
- SL and TP copied exactly
```

### TC3: Partial Close

```
Steps:
1. Open position on master
2. Partially close 50%
3. Verify partial close on follower

Expected:
- Follower position reduced by 50%
```

### TC4: Multiple Followers

```
Steps:
1. Connect 3 follower accounts to 1 master
2. Place trade on master
3. Verify all followers receive

Expected:
- All 3 accounts receive copy
- Each with correct lot sizing
```

### TC5: Rate Limiting

```
Steps:
1. Place 10 rapid trades on master (within 1 second)
2. Check rate limit handling

Expected:
- Excess orders queued or rejected gracefully
- No duplicate copies
```

## Test Execution

### Manual Testing

1. Login ke akun master.
2. Login ke akun follower di tab berbeda.
3. Buat copy profile via UI.
4. Place trade di master.
5. Verifikasi di follower.

### Automated Testing

```csharp
public class LiveCopyTradingTests
{
    [Fact]
    public async Task TC1_BasicCopy()
    {
        // Arrange
        var masterToken = await GetMasterToken();
        var followerToken = await GetFollowerToken();

        var profile = await CreateCopyProfileAsync(
            masterToken,
            followerToken,
            new CopyProfileSettings { CopyRatio = 1.0 });

        // Act
        var trade = await PlaceTradeAsync(masterToken, "EURUSD", 0.5, Direction.Buy);

        // Assert
        await Task.Delay(5000); // Wait for copy
        var followerTrades = await GetOpenTradesAsync(followerToken);

        Assert.Contains(followerTrades, t =>
            t.Symbol == "EURUSD" &&
            t.Direction == Direction.Buy &&
            Math.Abs(t.Lots - 0.5) < 0.01);
    }
}
```

## Verification

### Trade Matching

| Field | Master | Follower | Must Match |
|-------|--------|----------|------------|
| Symbol | EURUSD | EURUSD | Yes |
| Direction | Buy | Buy | Yes |
| Lots | 0.5 | ~0.5 | Yes (within ratio) |
| Entry Price | 1.0850 | ~1.0850 | Within slippage |
| SL | 1.0800 | 1.0800 | Yes |
| TP | 1.0950 | 1.0950 | Yes |

### Timing

| Metric | Target |
|--------|--------|
| Time to copy | < 5 seconds |
| Max slippage | 5 pips |
| Copy success rate | > 99% |

## Known Issues

### Issue 1: Rate Limit

cTrader API memiliki rate limit ketat. Jika exceeded:

```
Error: 429 Too Many Requests
Solution: Implement exponential backoff, queue excess orders
```

### Issue 2: Token Expiry

OAuth token expires setelah 1 jam. Jika expired:

```
Error: 401 Unauthorized
Solution: Implement automatic token refresh
```

### Issue 3: Account Not Linked

Jika akun belum ditautkan dengan benar:

```
Error: Account not linked
Solution: Re-connect account via OAuth flow
```

## Cleanup

Setelah testing, pastikan untuk:

1. Hapus semua test profiles.
2. Tutup semua test positions.
3. Revoke test tokens.
4. Reset test account balances.
