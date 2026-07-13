---
description: "API kalender ekonomi — baca event yang dijadwalkan, berlangganan webhook, dan gunakan dalam logika cBot."
---

# Kalender Ekonomi — cBot API

API kalender ekonomi — baca event yang dijadwalkan, berlangganan webhook, dan gunakan dalam logika cBot.

## Menyiapkan

Kalender ekonomi diaktifkan secara default. Jika dinonaktifkan, set
`App:Features:EconomicCalendar=true` di konfigurasi.

## Baca event

```csharp
// Dapatkan semua event dalam rentang tanggal
var events = await _calendarClient.GetEventsAsync(
    from: DateTime.UtcNow,
    to: DateTime.UtcNow.AddDays(7),
    cancellationToken);

// Filter berdasarkan impact
var highImpact = events.Where(e => e.Impact == Impact.High);

// Dapatkan event berikutnya untuk simbol
var eurEvents = events.Where(e => e.Currencies.Contains("EUR"));
```

## Webhook langganan

```csharp
// Daftar untuk notifikasi event
_calendarClient.SubscribeToEvents(event => {
    Console.WriteLine($"Event: {event.Title} - Impact: {event.Impact}");
});
```

## Gunakan dalam cBot

```csharp
public class NewsScalper : CBot
{
    private readonly IEconomicCalendar _calendar;

    public NewsScalper(IEconomicCalendar calendar)
    {
        _calendar = calendar;
    }

    protected override async Task OnTickAsync(Tick tick)
    {
        // Abaikan perdagangan di sekitar newshigh-impact
        var nearbyNews = _calendar.GetEventsInRange(tick.Time, TimeSpan.FromHours(1));
        if (nearbyNews.Any(e => e.Impact == Impact.High))
        {
            // Jangan trade di sekitar news
            return;
        }

        // Logika trading normal
        // ...
    }
}
```

## Struktur event

| Field | Tipe | Deskripsi |
|-------|------|-----------|
| `Id` | `string` | ID unik event |
| `Title` | `string` | Nama event (mis. "US Non-Farm Payrolls") |
| `Impact` | `Impact` | High / Medium / Low |
| `Currencies` | `string[]` | Mata uang yang terpengaruh |
| `Time` | `DateTimeOffset` | Waktu event |
| `Actual` | `double?` | Nilai aktual (null jika belum dirilis) |
| `Forecast` | `double?` | Konsensus forecast |
| `Previous` | `double?` | Nilai periode sebelumnya |

## Implementasi

- Event di-scrap dari sumber eksternal dan disimpan di database.
- cBot API menggunakan `IEconomicCalendarClient` (interface di Core) yang diimplementasikan
  di Infrastructure.
- Integrasi dengan `IAiFeatureService` untuk alert berbasis AI (berikutnya).
