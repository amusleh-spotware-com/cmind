---
description: "Kondycja strategii i rozpływ alfa — deterministyczne wykrywanie rozpływu porównujące ostatnie Sharpe'a strategii z jej wcześniejszym rekordem i lokalizujące największą zmianę średniej (CUSUM change-point), zwracające werdykt Healthy / Degrading / Decayed."
---

# Kondycja strategii a rozpływ alfa

Każda przewaga się rozpływa — badania są jasne, że półperiod rozpadu strategii quant skurczył się z lat do miesięcy, dlatego *adaptacja bije odkrycie*. Monitor Kondycji strategii mówi ci, z samej historii zwrotów strategii, czy przewaga wciąż istnieje.

Otwórz **cBots → Strategy Health** (`/quant/health`).

## Co robi

Mając serię zwrotów (lub krzywą kapitału, od najstarszej), wykonuje:

- dzieli historię na wcześniejszą i ostatnią połowę i porównuje ich współczynniki Sharpe'a;
- uruchamia skan **CUSUM change-point** w celu zlokalizowania obserwacji, w której średnia najbardziej wyraźnie się przesunęła (zmiana reżimu), zgłaszana tylko gdy odchylenie jest statystycznie godne uwagi;
- zwraca werdykt:

| Werdykt | Znaczenie |
|---|---|
| **Healthy** | Ostatnia wydajność jest zgodna z (lub lepsza niż) wcześniejszy rekord. |
| **Degrading** | Ostatni Sharpe jest zauważalnie słabszy niż wcześniejszy rekord — obserwuj uważnie. |
| **Decayed** | Przewaga skutecznie zniknęła w ostatnim oknie — rozważ wstrzymanie. |
| **Unknown** | Brak wystarczającej historii do oceny. |

- **Bezpośrednio z przebiegu backtestu — bez kopiowania i wklejania.** Każdy ukończony backtest ekspozycji ikony serca **Sprawdź kondycję strategii** na wierszu listy **Backtest** i na widoku szczegółów jego instancji; jedno kliknięcie uruchamia monitor na przechowywane krzywej kapitału tego przebiegu i pokazuje werdykt w oknie dialogowym. Ikona jest wyłączona, dopóki backtest się nie ukończy i nie wyprodukuje raportu, więc nigdy nie jest martwą kontrolką. Pod maską to jest `POST /api/quant/health/backtest/{instanceId}`, która odczytuje przechowywane krzywej kapitału z raportu.

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Dlaczego jest niezawodna

Jest czystym, deterministycznym kodem domeny (`Core.Health`) bez zależności infrastruktury i bez zewnętrznych wywołań — testowany dla przypadków rozpłynięcia, degradacji, zdrowia i zbyt krótkiej historii oraz dla lokalizacji change-pointu. Jest ręcznym towarzyszem zawsze włączonych sprawdzeń kondycji wspierających autonomicznych agentów: ta sama statystyka napędza wyłącznik obwodu, który zmniejsza ryzyko strategii na żywo, której przewaga zanika.
