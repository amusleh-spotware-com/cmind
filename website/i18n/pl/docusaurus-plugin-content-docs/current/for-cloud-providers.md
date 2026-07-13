---
slug: /for-cloud-providers
title: cMind dla dostawców chmury i VPS
description: Dlaczego dostawca chmury lub VPS powinien oferować hostingi zarządzany cMind — gotowy, zróżnicowany produkt dla traderów algo, brokerów i prop firm, z jasnymi sposobami zarabiania mocy obliczeniowej, white-label i AI.
keywords:
  - Hostingi zarządzany
  - Dostawca VPS
  - Dostawca chmury
  - Hosting platformy handlowej
  - Odsprzedawca white-label
  - Zarządzany hosting AI
sidebar_position: 7
---

# cMind dla dostawców chmury i VPS 🖥️

Już wynajmujesz moc obliczeniową. cMind to gotowy, otwarte oprogramowanie produkt, który możesz owinąć tę moc obliczeniową wokół: **oferuj zarządzany hosting cMind** i wyląduj wysokowartościowe, lepkie, żelazne obciążenie — handlowcy algorytmiczni, brokerzy, prop firmy i społeczności handlowe, które chcą platformy działającej bez bycia zespołem ops.

:::tip TL;DR
Uruchom warstwę bezstanową + Postgres + flotę węzłów; ręka klientów oznakowana URL. Zarabiaj na subskrypcji, mocy obliczeniowej, white-labelu i AI. → [Wdrażaj do chmury](./deployment/cloud.md)
:::

## Dlaczego oferować zarządzany cMind

- **Brak kosztu kompilacji.** Jest to otwarte oprogramowanie, MIT-licencjonowane i już udokumentowane, przetestowane i konteneryzowane. Pakujesz i obsługujesz — nie budujesz.
- **Zróżnicowany produkt dla dochodowej niszy.** Handel algorytmiczny pochłania CPU: backtesty i żywe węzły spalają CPU, co jest *mierzalnym użytkowaniem* już sprzedajesz.
- **Lepcy klienci.** Handlowcy, którzy budują i uruchamiają strategie wewnątrz platformy nie rezygnują z lekkomyślności.
- **Zmienia zastrzeżenie w upsell.** cMind jest samodzielnie hostowany z projektu — dla klientów, którzy "nie chcą być zespołem ops," *ty* jesteś odpowiedź.

## Kto kupuje zarządzany cMind od ciebie

- **Indywidualni quants i handlowcy** którzy to chcą hostować. → [Dla traderów](./for-traders.md)
- **Brokerzy cTrader** prowadzący white-label dla swoich klientów. → [Dla brokerów](./for-brokers.md)
- **Prop firmy i biznesu kopii handlowej** którzy potrzebują oznakowanej, audytowańskiej infrastruktury.

## Co "zarządzany cMind" oznacza do uruchomienia

Obsługujesz trzy warstwy; klient uzyskuje oznakowany URL sieciowy:

| Warstwa | Co to jest | Gdzie to działa |
|---|---|---|
| Bezstanowy (Web + MCP) | Aplikacja + API + serwer MCP | Dowolna platforma kontenerów, autoskalowana |
| Baza danych | PostgreSQL | Zarządzany Postgres (RDS / Flexible Server / twój własny) |
| Flota węzłów | Kompiluje i uruchamia kontenery cTrader | **VM lub Kubernetes — wymaga uprzywilejowanego Docker** |

:::warning Jedna rzecz do zakresu z przodu
Agenci węzłów kompilują i uruchamiają kontenery cTrader, więc potrzebują **uprzywilejowanego Docker**. To wyklucza bezserwerowe runtimes kontenera (Azure Container Apps, AWS Fargate) *dla agentów* — uruchamia je na [Kubernetes](./deployment/kubernetes.md), VM lub EC2. Warstwa bezstanowa działa wszędzie.
:::

Rzeczywiste, copy-paste przewodniki wdrażania to ukonkretnić: [przegląd chmury](./deployment/cloud.md) · [Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) · [Kubernetes](./deployment/kubernetes.md) · [Skalowanie](./deployment/scaling.md).

## Jak to zarabiasz

- **Subskrypcja hostingu zarządzanego.** Plany miesięczne Starter / Team / Business rozmiarów przez flotę węzłów i równoległy backtest.
- **Użycie i metryka mocy obliczeniowej.** Rachunek backtest-godzin, żywy-węzeł-godzin i przechowywania — naturalnie mierzone flotą kontenerów już uruchamiasz.
- **Warstwy odsprzedawcy white-label.** Pobieraj więcej za pełny rebrand (logo, kolory, PWA, `ShowSiteLink=false`) i umożliwianie możliwości premium poprzez [przełączniki funkcji](./features/feature-toggles.md). → [White-label](./features/white-label.md)
- **Zarządzany AI.** Pakuj domyślny klucz dostawcy AI, aby każdy użytkownik klienta uzyskał AI bez konfiguracji, i zaznacz użycie — lub oferuj przynieś-własny-klucz. → [Funkcja AI](./features/ai.md)
- **Prop-firm i kopia handlowa przychód udostępniania.** Hostuj firmy prowadzące wyzwania i opłaty za wydajność i weź część platformy. → [Prop-firm](./features/prop-firm.md) · [Opłaty za wydajność](./features/copy-performance-fees.md) · [Rynek dostawcy](./features/copy-provider-marketplace.md)
- **Konfiguracja, onboarding i SLA.** Dołącz usługi profesjonalne i wsparcie premium.

## Wielodostępne wzorce

- **Wdrażanie na dzierżawcę (rekomendowane).** Jedna instancja oznakowana na klienta — silna izolacja, branding na dzierżawcę i baza danych, token join węzła odrębny na dzierżawcę. Branding jest odczytywany z `IOptionsMonitor`, więc każda instancja przenosi swoją tożsamość. → [Branding wielodostępny](./white-label-for-business.md#multi-tenant-per-customer-branding) · [Odkrycie węzła](./operations/node-discovery.md)
- **Współdzielona płaszczyzna kontrolna (zaawansowana).** Napędzaj wiele instancji z twojej własnej warstwy obsługi administracyjnej, zasiewania branding i funkcji na dzierżawcę programowo.

## Metryka użycia dla rozliczeń

Właściciel/admin-only **`GET /api/usage`** endpoint zwraca przeczytaj podsumowanie dostawcy może ankietę i rachunek — bez żadnej nowej domeny lub trwałości, projektuje istniejący stan:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Ankietę ją dzierżawcy wdrażania do napędu na siedzenie, flotę-based lub obciążenie-based ceny. Pary z [logowaniem i obserwowaniem](./operations/logging.md) dla dokładniejszego metryka obliczeniowego.

## Utrzymanie marż przewidywalne

Skaluj węzły na żądanie, dziel warstwy Postgres i autoskaluj warstwę bezstanową. Powierzchnie operacyjne, które potrzebujesz są już tam:

- [Skalowanie i samo-leczenie](./deployment/scaling.md)
- [Logowanie i obserwowanie](./operations/logging.md)
- [Kopia zapasowa i odzyskiwanie](./operations/backup-recovery.md)

## Zacznij

1. Stanowisko wdrażania odniesienia z [przewodników chmury](./deployment/cloud.md).
2. Szablon to dzierżawcy (branding + token join + DB) i drut rozliczenia do użycia obliczeniowego.
3. Lista to — teraz masz zarządzaną platformę handlu algo do sprzedania.

## Wniesież wkład do przodu

Dostawcy uruchamiający cMind w skali trafiają ostre krawędzie pierwszy. Upublicznych operacyjne naprawy i ulepszenia IaC utrzymuje flotę tanią dla utrzymania — zacznij od [Przewodnika współtworzenia](./contributing.md).
