---
slug: /white-label-for-business
title: White-label dla biznesu
description: Wyślij cMind jako własny produkt oznaczony — dla prop firm, brokerów i biznesu kopii handlowej. Rebranding każdej powierzchni poprzez config, bez zmian kodu.
sidebar_position: 4
---

# White-label cMind dla twojego biznesu 🏢

Prowadzisz prop firmę, biuro brokera lub usługę kopii handlowej? cMind był zbudowany od dnia pierwszego, aby być **odsprzedawany jako twój własny produkt**. Każda powierzchnia — nazwa, logo, favicon, kolory, nawet instalowalna aplikacja na telefon — podpada się pod twoją markę. Twoi klienci widzą *twoją* firmę. Żadnych zmian kodu, żadnego rozwidlenia, tylko config.

:::tip[TL;DR]
Wskaż `App:Branding` na swoją nazwę, kolory i logo. Ponownie uruchom. Gotowe. Pełne odniesienie techniczne żyje w [dokumencie funkcji White-label](./features/white-label.md).
:::

## Co możesz rebranding

| Powierzchnia | Co się zmienia |
|---|---|
| **Nazwa produktu** | Tekst paska aplikacji + tytuł karty przeglądarki |
| **Logo i favicon** | Twoje znaki wszędzie, w tym karta przeglądarki |
| **Kolory** | Pełna paleta — główna, powierzchnie, kolory statusu — przepływa przez cały interfejs użytkownika *i* własny CSS aplikacji poprzez tokeny projektowe |
| **Instalowalna aplikacja (PWA)** | Nazwa, ikona i wypluskanie dodawania do ekranu głównego używają twojej marki |
| **Meta / SEO** | Opis i adres URL pomocy są twoimi |
| **Niestandardowy CSS** | Wstrzyknij swoją własną polerkę na ostatnie 5% |

Wszystko domyślnie tożsamością cMind, więc tylko przesłaniasz to, na którym ci zależy.

## 60-sekundowy rebrand

Ustaw je na swoim wdrażaniu (JSON config lub zmienne środowiskowe):

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

Zmienna środowiskowa form: `App__Branding__ProductName=AcmeFX`. Kolory są sprawdzane przy uruchamianiu — zła wartość hex nie przebą boot z jasną wiadomością zamiast renderowania złamanej strony. Ładnie i głośno, dokładnie kiedy tego chcesz.

## Link "Powered by cMind"

Domyślnie pulpit pokazuje małą, smaczną **link "Powered by cMind"**, który wskazuje zwiedzających z powrotem do tej strony. Jest to domyślnie, ponieważ jesteśmy dumni z projektu i to pomaga innym handlowcom go znaleźć — ale to **twój wybór**.

- **Utrzymaj to** (domyślnie): link kredytu subtelne na pulpicie. Nic ciebie nie kosztuje, pomaga projektowi.
- **Ukryj to**: ustaw `App__Branding__ShowSiteLink=false` i znika całkowicie — idealne dla w pełni white-labelowego wdrażania, gdzie produkt jest wyraźnie *twój*.

Patrz [dokument funkcji white-label](./features/white-label.md#powered-by-link) dla dokładnie gdzie renderuje.

## Wielodostępne, branding na klienta

Ponieważ branding jest tylko config wdrażania, każde wdrażanie dzierżawcy może przenosić swoją tożsamość. Uruchom oddzielną instancję na klienta, lub napędzaj branding z twojej własnej płaszczyzny kontrolnej — aplikacja czyta go z `IOptionsMonitor`, więc może nawet przebudować temat na żywo, kiedy opcje się zmienią.

Pary z:

- **[Przełączniki funkcji](./features/feature-toggles.md)** — zdecyduj, które możliwości każdy dzierżawca widzi.
- **[Reguły prop-firm](./features/prop-firm.md)** — egzekwuj swoje reguły wyzwania z śledzeniem kapitału na żywo.
- **[Opłaty za wydajność](./features/copy-performance-fees.md)** + **[rynek dostawcy](./features/copy-provider-marketplace.md)** — zarabiaj na kopii handlowej.
- **[Zgodność](./features/compliance.md)** — utrzymaj ścieżkę audytu, którą poprosi twój regulator.

## Majątek i hosting

Upuść logo/favicon do aplikacji sieci Web `wwwroot/branding/` (lub wskaż `LogoUrl`/`FaviconUrl` na dowolny bezwzględny URL). Wdrażaj jak się wam podoba — [Docker](./deployment/local.md), [Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md) lub [AWS](./deployment/cloud-aws.md).

Gotowy to uczynić swoim? Zacznij od [technicznego odniesienia white-label →](./features/white-label.md)
