---
description: "White-label owner settings — semua opsi white-label dapat dikonfigurasi oleh owner di Settings → Deployment."
---

# White-Label Owner Settings

White-label owner settings — semua opsi white-label dapat dikonfigurasi oleh owner di Settings → Deployment.

## Overview

Owner dapat mengonfigurasi semua aspek white-label tanpa redeploy atau restart:

- **Branding** — logo, warna, nama.
- **Fitur** — aktif/nonaktif fitur.
- **Registrasi** — syarat dan konfigurasi.
- **Email** — template dan pengaturan.
- **Accounts** — tipe akun yang tersedia.
- **AI** — pengaturan AI dan limits.
- **Prop Firm** — aturan dan batas.

## Akses

**Settings → Deployment** (hanya untuk owner):

```
Deployment Settings
├── Branding
├── Features
├── Registration
├── Email
├── Accounts
├── AI Settings
└── Prop Firm
```

## Branding

### Logo

```json
{
  "branding": {
    "logoUrl": "https://cdn.example.com/logo.png",
    "logoAlt": "My Trading Platform",
    "faviconUrl": "https://cdn.example.com/favicon.ico"
  }
}
```

### Colors

```json
{
  "branding": {
    "primaryColor": "#1E88E5",
    "secondaryColor": "#424242",
    "accentColor": "#FFC107",
    "errorColor": "#D32F2F"
  }
}
```

### App Name

```json
{
  "branding": {
    "appName": "My Trading Platform",
    "companyName": "My Trading Company Ltd",
    "supportEmail": "support@example.com"
  }
}
```

## Features

### Feature Flags

```json
{
  "features": {
    "copyTrading": true,
    "backtest": true,
    "ai": true,
    "economicCalendar": true,
    "propFirm": false,
    "twoFactorAuth": true
  }
}
```

### Feature Limits

```json
{
  "features": {
    "maxAccounts": 10,
    "maxCBots": 50,
    "maxBacktestsPerDay": 100,
    "aiRequestsPerDay": 1000
  }
}
```

## Registration

### Open vs Invite-Only

```json
{
  "registration": {
    "mode": "invite-only",
    "inviteOnly": true,
    "allowedDomains": ["company.com", "partner.com"]
  }
}
```

### Required Fields

```json
{
  "registration": {
    "requiredFields": ["email", "firstName", "lastName"],
    "optionalFields": ["phone", "company"],
    "emailVerification": true,
    "minPasswordLength": 12
  }
}
```

## Email

### Templates

```json
{
  "email": {
    "fromAddress": "noreply@example.com",
    "fromName": "My Trading Platform",
    "templates": {
      "welcome": "welcome-template-id",
      "passwordReset": "reset-template-id",
      "verification": "verify-template-id"
    }
  }
}
```

### SMTP

```json
{
  "email": {
    "smtp": {
      "host": "smtp.example.com",
      "port": 587,
      "username": "smtp-user",
      "password": "encrypted-password"
    }
  }
}
```

## Accounts

### Account Types

```json
{
  "accounts": {
    "types": [
      {
        "name": "Demo",
        "isDemo": true,
        "defaultBalance": 10000
      },
      {
        "name": "Live",
        "isDemo": false,
        "minDeposit": 500
      }
    ]
  }
}
```

## AI Settings

### Providers

```json
{
  "ai": {
    "defaultProvider": "anthropic",
    "providers": {
      "anthropic": {
        "enabled": true,
        "apiKey": "encrypted-key"
      },
      "openai": {
        "enabled": false
      }
    }
  }
}
```

### Limits

```json
{
  "ai": {
    "limits": {
      "requestsPerDay": 1000,
      "requestsPerMinute": 10,
      "maxTokensPerRequest": 4096
    }
  }
}
```

## Prop Firm

### Rules

```json
{
  "propFirm": {
    "enabled": true,
    "challengeTypes": [
      {
        "name": "Evaluation",
        "durationDays": 30,
        "maxDailyLoss": 0.05,
        "maxTotalLoss": 0.10,
        "profitTarget": 0.10
      }
    ],
    "defaultChallenge": "Evaluation"
  }
}
```

## Security

### Password

```json
{
  "security": {
    "minPasswordLength": 12,
    "requireUppercase": true,
    "requireLowercase": true,
    "requireNumber": true,
    "requireSpecialChar": true,
    "passwordExpiryDays": 90
  }
}
```

### Session

```json
{
  "security": {
    "sessionTimeout": "00:30:00",
    "maxConcurrentSessions": 3,
    "requireMfa": false
  }
}
```

## Live Updates

Semua perubahan berlaku langsung tanpa restart:

```csharp
// Perubahan di database
await _db.WhiteLabelSettings.UpdateAsync(settings);

// Cache invalidation
_cache.Invalidate("WhiteLabelSettings");

// Applied on next request
var effectiveSettings = _optionsMonitor.Get(Options.WhiteLabel);
```

## Audit

Semua perubahan dicatat:

```json
{
  "audit": {
    "changedAt": "2024-01-15T10:30:00Z",
    "changedBy": "owner-123",
    "field": "features.copyTrading",
    "oldValue": "false",
    "newValue": "true"
  }
}
```
