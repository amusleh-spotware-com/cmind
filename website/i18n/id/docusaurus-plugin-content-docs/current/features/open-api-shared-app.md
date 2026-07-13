---
description: "Shared Open API App — satu aplikasi OAuth terpusat untuk semua user, dengan rate limit per-message-type."
---

# Open API Shared App

Shared Open API App — satu aplikasi OAuth terpusat untuk semua user, dengan rate limit per-message-type.

## Arsitektur

Alih-alih setiap user membuat aplikasi OAuth sendiri, cMind menggunakan satu aplikasi OAuth terpusat:

```
User → cMind (OAuth) → cTrader Open API
```

Ini menyederhanakan:

- **Credential management** — satu set kredensial.
- **Rate limiting** — dikelola per-message-type.
- **Compliance** — satu aplikasi, satu persetujuan.

## Setup

### Initial Setup

1. Buat aplikasi di cTrader Developer Portal.
2. Dapatkan `client_id` dan `client_secret`.
3. Masukkan ke cMind configuration:

```json
{
  "OpenApi": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  }
}
```

### User Connection

User menghubungkan akun cTrader mereka:

1. Buka **Settings → Connected Accounts**.
2. Klik **Connect cTrader Account**.
3. Diarahkan ke OAuth flow cTrader.
4. Berikan persetujuan.
5. Akun tertaut.

## Token Management

### Storage

Token dienkripsi menggunakan `ISecretProtector`:

```csharp
var encryptedToken = _protector.Protect(oauthToken);
await _db.SaveAsync(encryptedToken);
```

### Refresh

Token di-refresh otomatis sebelum expiry:

```csharp
if (token.ExpiresAt < DateTime.UtcNow.AddMinutes(5))
{
    var newToken = await _openApiClient.RefreshTokenAsync(token.RefreshToken);
    await SaveTokenAsync(newToken);
}
```

### Rotation

Token di-rotasi reguler:

- **Access token** — 1 jam validity, refresh setiap 30 menit.
- **Refresh token** — 30 hari validity, rotate setiap use.

## Rate Limiting

### Per-Message-Type

| Message Type | Limit/minute |
|--------------|--------------|
| `orderCreate` | 30 |
| `orderModify` | 60 |
| `orderCancel` | 60 |
| `positionClose` | 30 |
| `positionModify` | 60 |
| `balanceRequest` | 120 |

### Implementation

Rate limiter menggunakan token bucket:

```csharp
public class RateLimiter
{
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets;

    public async Task<bool> TryAcquire(string key, int cost = 1)
    {
        var bucket = _buckets.GetOrAdd(key, _ => new TokenBucket(60, 60));
        return await bucket.TryConsumeAsync(cost);
    }
}
```

### Handling 429

```csharp
if (response.StatusCode == 429)
{
    var retryAfter = response.Headers.RetryAfter;
    await Task.Delay(retryAfter);
    // Retry
}
```

## User Isolation

Meskipun satu aplikasi, setiap user tetap terisolasi:

### Data Access

```csharp
// Cek ownership
var account = await _db.Accounts
    .Where(a => a.UserId == userId && a.Id == accountId)
    .FirstOrDefaultAsync();

if (account == null)
    throw new UnauthorizedAccessException();
```

### Webhook Verification

Webhook signature diverifikasi:

```csharp
public bool VerifyWebhook(OpenApiWebhook webhook)
{
    var signature = ComputeHmacSha256(webhook.Payload, _clientSecret);
    return signature == webhook.Signature;
}
```

## Monitoring

Dashboard menampilkan:

- **Token status** — valid/expired/refreshing.
- **Rate limit usage** — penggunaan per user dan per message type.
- **API errors** — error rate dan type.
- **Latency** — response time ke Open API.

## Troubleshooting

### Token Expired

```
Error: token_expired
Fix: Token akan di-refresh otomatis. Jika persist, reconnect akun.
```

### Rate Limited

```
Error: rate_limit_exceeded
Fix: Kurangi request rate atau upgrade tier.
```

### Webhook Signature Invalid

```
Error: invalid_signature
Fix: Verifikasi client secret dan webhook payload.
```
