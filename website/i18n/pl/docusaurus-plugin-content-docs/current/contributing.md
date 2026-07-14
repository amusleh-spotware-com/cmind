---
slug: /contributing
title: Współtworzenie
description: Jak wnieść wkład do cMind — PR wspierane przez człowieka lub AI są mile widziane. Pierwszy wkład w 10 minut.
sidebar_position: 5
---

# Współtworzenie cMind 🛠️

Dziękuję za to, że jesteś tutaj. cMind staje się lepszy za każdym razem, gdy ktoś otwiera problem, raport dokładne zachowanie cTrader, naprawia literówkę w tych samych dokumentach lub wysyła PR. **Nie musisz być czarodziejem .NET** — testerzy, traderzy i osoby naprawiające dokumenty są tak cenne, jak osoby piszące agregaty.

:::tip[Kanoniczny przewodnik żyje w repo]
Ta strona jest przyjaznym wjazdem. Pełny, zawsze bieżący proces — podstawowe reguły, konwencje kodowania, przepływ przeglądu — znajduje się w **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Twój pierwszy wkład w ~10 minut

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 ostrzeżeń, lub CI uprzejmie cię odmówi
dotnet test           # jednostka + integracja + E2E
```

Coś znaleźliśmy do naprawy? Rozgałęź to, zmień to, dodaj test i otwórz PR. To cała pętla.

## Sposoby na pomoc (nie wszystkie to kod)

| Wkład | Wysiłek | Gdzie |
|---|---|---|
| 🐛 Zgłoś odtwarzalny błąd | 10 min | [Raport o błędzie](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Zasugeruj funkcję | 10 min | [Żądanie funkcji](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Ulepsz te dokumenty | 15 min | Edytuj pod `website/docs/` i PR |
| 🧪 Dodaj brakujący test | 30 min | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Raportuj dokładne zachowanie cTrader | 10 min | [Otwórz dyskusję](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Zasady domu (krótka wersja)

cMind przenosi **rzeczywiste pieniądze**, więc kilka rzeczy jest nie do negocjacji — i szczerze mówiąc, sprawiają, że baza kodu jest radością do pracy:

- **Ścisłe Domain-Driven Design.** Logika biznesowa żyje na agregatach i obiektach wartości, nigdy w endpointach lub interfejsie użytkownika. (W repo jest przyjazny podręcznik dla niego.)
- **Trzy poziomy testów, każda zmiana.** Jednostka + integracja + E2E, *w tym* ścieżki błędów (upuszczone połączenia, odrzucone zamówienia, martwe węzły). Zielone testy to cena przyjęcia.
- **Zero ostrzeżeń.** `TreatWarningsAsErrors=true`. Nowoczesne idiomy C# 14.
- **Brak sekretów, brak magicznych ciągów, nigdy `DateTime.UtcNow`** (zamiast tego wtryskaj `TimeProvider`).
- **Dokumenty w tym samym commit. Zmień zachowanie → zaktualizuj jego dokumentację. Tak, to obejmuje tę stronę.

Pełny szczegół, z *dlaczego* za każdą regułą, w [CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) i [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Współtworzenie z AI 🤖

Naprawdę witamy **PR wspierane przez AI** — ten projekt jest zbudowany do pracy z agentami, a także ludźmi. Jeśli jesteś kierownikiem Claude, Copilot lub podobnym: wskaż go na [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), pozwól mu przeczytać zagnieżdżone pliki `CLAUDE.md` i utrzymuj go na tej samej pasku (testy, zero ostrzeżeń, DDD). Dobry PR AI nie różni się od dobrego PR człowieka — ta sama recenzja, ta sama powitania.

## Bądź doskonały dla siebie nawzajem

Mamy [Kodeks postępowania](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md). Gist: bądź miły, zakładaj dobrą wiarę i pamiętaj, że na drugiej stronie jest osoba (lub agent osoby). Zadawaj pytania wcześnie — to mocna strona, nie problem.

Witaj na pokładzie. Nie możemy się doczekać, aby zobaczyć, co budujesz. 🎉
