---
description: "Lokalisasi — dukungan 23 bahasa, RTL, format tanggal/nomor spesifik locale."
---

# Lokalisasi

Lokalisasi — dukungan 23 bahasa, RTL, format tanggal/nomor spesifik locale.

## Bahasa yang Didukung

cMind mendukung 23 bahasa:

| Bahasa | Kode |
|--------|------|
| English | en |
| Arabic | ar |
| Chinese (Simplified) | zh-Hans |
| Chinese (Traditional) | zh-Hant |
| Czech | cs |
| German | de |
| Greek | el |
| Spanish | es |
| French | fr |
| Hungarian | hu |
| Indonesian | id |
| Italian | it |
| Japanese | ja |
| Korean | ko |
| Malay | ms |
| Polish | pl |
| Portuguese (Brazil) | pt-BR |
| Russian | ru |
| Slovak | sk |
| Slovenian | sl |
| Serbian | sr |
| Thai | th |
| Turkish | tr |
| Vietnamese | vi |

## Mengubah Bahasa

### UI

1. Buka **Settings → Language**.
2. Pilih bahasa dari dropdown.
3. UI langsung berubah tanpa refresh.

### API

```http
POST /api/set-culture
{
  "culture": "id"
}
```

### Browser

Bahasa diatur otomatis dari `Accept-Language` header browser.

## RTL Support

### Bahasa RTL

Arabic adalah RTL (right-to-left):

```razor
<MudRTLProvider>
    <MudThemeProvider />
    <!-- App content -->
</MudRTLProvider>
```

HTML `dir` attribute diatur otomatis:

```html
<html dir="rtl" lang="ar">
```

### Considerations

- Semua komponen UI mendukung RTL.
- Chart dan visualisasi di-flip otomatis.
- Ikon tertentu perlu di-mirror.

## Format

### Number

| Locale | Contoh |
|--------|--------|
| en-US | 1,234.56 |
| de-DE | 1.234,56 |
| id-ID | 1.234,56 |

### Date

| Locale | Contoh |
|--------|--------|
| en-US | 01/15/2024 |
| de-DE | 15.01.2024 |
| id-ID | 15/01/2024 |

### Currency

| Locale | Contoh |
|--------|--------|
| en-US | $1,234.56 |
| de-DE | 1.234,56 € |
| id-ID | Rp 1.234 |

## Translation Keys

### Format

```json
{
  "key": "value",
  "dashboard_title": "Dashboard",
  "copy_trading": "Copy Trading",
  "settings_saved": "Settings saved successfully"
}
```

### Di Code

```csharp
// Inject IStringLocalizer
private readonly IStringLocalizer<Ui> _l;

// Gunakan
var title = _l["dashboard_title"];
var message = _l["settings_saved"];
```

### Di Razor

```razor
@L["dashboard_title"]
@L["copy_trading"]
```

## Membuat Translation Baru

1. **Tambah key** ke `tools/i18n/ui-translations.json`.
2. **Generate** resource files:

```bash
pwsh tools/i18n/gen-resx.ps1
```

3. **Terjemahkan** semua file `.resx` di `src/Web/Resources/`.
4. **Build** — resource parity gate memastikan semua bahasa lengkap.

## Fallback

Jika translation key tidak ditemukan:

1. Cek bahasa spesifik (mis. `id-ID`).
2. Cek language tanpa region (mis. `id`).
3. Fallback ke English.

## Testing

### Visual Check

- Setiap halaman dengan teks UI.
- Format number, date, currency.
- RTL layout.

### Automated Tests

`ResourceParityTests` memastikan:
- Tidak ada key yang kosong.
- Semua bahasa memiliki jumlah key yang sama.
