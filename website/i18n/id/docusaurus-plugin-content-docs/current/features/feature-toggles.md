---
description: "Feature toggles — kontrol fitur individual pada runtime tanpa redeploy."
---

# Feature Toggles

Feature toggles — kontrol fitur individual pada runtime tanpa redeploy.

## Overview

Feature toggles memungkinkan Anda mengaktifkan/menonaktifkan fitur tanpa redeploy atau restart.
Ini berguna untuk:

- **Gradual rollouts** — aktifkan fitur untuk % pengguna.
- **A/B testing** — test fitur baru dengan grup kecil.
- **Kill switch** — matikan fitur bermasalah segera.
- **Beta programs** — akses awal untuk pengguna tertentu.

## Toggle Types

### Release Toggles

Mengaktifkan/menonaktifkan fitur yang sedang development:

```json
{
  "toggle": "new-dashboard",
  "type": "release",
  "enabled": false,
  "description": "Dashboard baru dengan chart yang lebih baik"
}
```

### Experiment Toggles

Untuk A/B testing:

```json
{
  "toggle": "checkout-flow-v2",
  "type": "experiment",
  "enabled": true,
  "variants": ["control", "treatment"],
  "weights": [50, 50],
  "description": "Aliran checkout baru"
}
```

### Operational Toggles

Kill switch untuk operasi:

```json
{
  "toggle": "copy-trading",
  "type": "operational",
  "enabled": true,
  "description": "Fitur copy-trading utama"
}
```

### Permission Toggles

Berdasarkan user permission:

```json
{
  "toggle": "beta-features",
  "type": "permission",
  "enabled": false,
  "allowedRoles": ["beta-tester", "admin"],
  "description": "Fitur beta untuk tester"
}
```

## Menggunakan Toggles

### Di Code

```csharp
// Cek toggle
if (_featureGate.IsEnabled("new-dashboard"))
{
    return new DashboardV2();
}
return new DashboardV1();

// Atau dengan default
if (_featureGate.IsEnabled("copy-trading", defaultValue: true))
{
    // copy-trading on by default
}

//Dengan variant untuk experiment
var variant = _featureGate.GetVariant("checkout-flow-v2");
return variant == "treatment" ? new CheckoutV2() : new CheckoutV1();
```

### Di Razor

```razor
@if (await FeatureGate.IsEnabledAsync("new-dashboard"))
{
    <NewDashboard />
}
else
{
    <OldDashboard />
}
```

## Management UI

**Settings → Features** menampilkan semua toggle:

| Toggle | Type | Status | Actions |
|--------|------|--------|---------|
| new-dashboard | release | Off | Edit / Enable |
| copy-trading | operational | On | Edit / Disable |
| checkout-v2 | experiment | 50/50 | Edit / Disable |

### Actions

- **Enable** — aktifkan untuk semua.
- **Disable** — nonaktifkan untuk semua.
- **Edit** — ubah konfigurasi.
- **Delete** — hapus toggle (hanya jika tidak digunakan).

## Configuration

### File-Based

`appsettings.json`:

```json
{
  "FeatureToggles": {
    "new-dashboard": false,
    "copy-trading": true
  }
}
```

### Database

Toggle yang disimpan di database dapat diubah melalui UI tanpa redeploy.

### Environment Variables

```bash
FeatureToggle__NewDashboard=true
FeatureToggle__CopyTrading=false
```

## Best Practices

1. **Meaningful names** — nama yang jelas menunjukkan fungsi.
2. **Document purpose** — selalu ada description.
3. **Cleanup old toggles** — hapus toggle yang tidak digunakan.
4. **Default to off** — untuk release toggles, default off.
5. **Monitor usage** — track siapa yang menggunakan fitur.
