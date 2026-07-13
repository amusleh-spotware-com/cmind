---
description: "Token lifecycle — provisioning, rotation, expiry, dan revocation kredensial API."
---

# Token Lifecycle

Token lifecycle — provisioning, rotation, expiry, dan revocation kredensial API.

## Overview

cMind mengelola beberapa tipe token:

| Tipe | Penggunaan | Lifetime |
|------|-----------|---------|
| **JWT** | API authentication | 5 menit |
| **Refresh Token** | Mendapatkan JWT baru | 30 hari |
| **OAuth Token** | cTrader Open API | 1 jam |
| **MCP Token** | MCP server access | Konfigurabel |

## JWT Tokens

### Generation

JWT dihasilkan saat login:

```csharp
public string GenerateJwt(User user)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim("role", user.Role)
    };

    var key = new SymmetricSecurityKey(_jwtSecret);
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: "cmind",
        audience: "cmind-api",
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(5),
        signingCredentials: creds
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

### Validation

```csharp
public async Task<ClaimsPrincipal> ValidateJwt(string token)
{
    var handler = new JwtSecurityTokenHandler();
    var key = new SymmetricSecurityKey(_jwtSecret);

    var parameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = "cmind",
        ValidateAudience = true,
        ValidAudience = "cmind-api",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    return await handler.ValidateAsync(token, parameters);
}
```

## Refresh Tokens

### Rotation

Refresh token di-rotasi setiap use:

```csharp
public async Task<TokenPair> RotateRefreshToken(string refreshToken)
{
    var userId = await ValidateRefreshToken(refreshToken);

    // Invalidate old token
    await _db.RefreshTokens
        .Where(t => t.Token == refreshToken)
        .DeleteAsync();

    // Generate new pair
    var jwt = GenerateJwt(userId);
    var newRefresh = GenerateRefreshToken();
    await SaveRefreshToken(userId, newRefresh);

    return new TokenPair(jwt, newRefresh);
}
```

### Storage

Token di-hash sebelum disimpan:

```csharp
public async Task SaveRefreshToken(string userId, string token)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
    var entity = new RefreshToken
    {
        UserId = userId,
        TokenHash = Convert.ToBase64String(hash),
        ExpiresAt = DateTime.UtcNow.AddDays(30),
        CreatedAt = DateTime.UtcNow
    };
    await _db.RefreshTokens.AddAsync(entity);
}
```

## OAuth Tokens

### cTrader Open API

```csharp
public class OAuthTokenManager
{
    public async Task<OAuthToken> RefreshOAuthTokenAsync(string refreshToken)
    {
        var response = await _httpClient.PostAsync("https://api.spotware.com/oauth/token",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret)
            }));

        var token = await response.Content.ReadFromJsonAsync<OAuthToken>();
        await SaveTokenAsync(token);
        return token;
    }
}
```

### Automatic Refresh

Token di-refresh sebelum expiry:

```csharp
public class TokenRefreshBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var expiringTokens = await _db.OAuthTokens
                .Where(t => t.ExpiresAt < DateTime.UtcNow.AddMinutes(5))
                .ToListAsync();

            foreach (var token in expiringTokens)
            {
                await _tokenManager.RefreshOAuthTokenAsync(token.RefreshToken);
            }

            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }
}
```

## MCP Tokens

### Generation

```csharp
public string GenerateMcpToken(User user)
{
    return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
```

### Scoping

```csharp
public class McpToken
{
    public string Token { get; set; }
    public string UserId { get; set; }
    public string[] Scopes { get; set; }  // "trading:read", "trading:write", "backtest:run"
    public DateTime ExpiresAt { get; set; }
}
```

## Revocation

### Manual Revocation

```csharp
public async Task RevokeToken(string token)
{
    await _db.RevokedTokens.AddAsync(new RevokedToken
    {
        TokenHash = Hash(token),
        RevokedAt = DateTime.UtcNow,
        Reason = "user_requested"
    });
}
```

### Automatic Revocation

```csharp
// Revoke all tokens for user
public async Task RevokeAllUserTokens(string userId)
{
    await _db.RefreshTokens.Where(t => t.UserId == userId).DeleteAsync();
    await _db.McpTokens.Where(t => t.UserId == userId).DeleteAsync();
    await _db.RevokedTokens.AddRangeAsync(GetActiveTokensForUser(userId).Select(t => new RevokedToken
    {
        TokenHash = Hash(t),
        RevokedAt = DateTime.UtcNow,
        Reason = "user_logout_all"
    }));
}
```

### Token Blacklist Check

```csharp
public async Task<bool> IsTokenRevoked(string token)
{
    var hash = Hash(token);
    return await _db.RevokedTokens.AnyAsync(t => t.TokenHash == hash);
}
```

## Monitoring

Dashboard menampilkan:

- **Active tokens** — jumlah token aktif per user.
- **Expiring soon** — token akan expire dalam 24 jam.
- **Revoked tokens** — token yang di-revoke.
- **Token usage** — pattern penggunaan token.
