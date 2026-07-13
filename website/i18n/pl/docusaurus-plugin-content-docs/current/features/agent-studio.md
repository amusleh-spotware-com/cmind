---
description: "Agent Studio — twórz agentów handlowych napędzanych personą, bez kodowania, z charakterem i archetypem zarządzającym kontami zgodnie z Twoimi celami pod nadzorem Jądra Autonomii i Bezpieczeństwa (zakonczenie ryzyka, wyłącznik, przycisk awaryjny, wersjonowane potwierdzenie disclaimer)."
---

# Agent Studio

Agent Studio pozwala ci utworzyć **agenta handlowego z charakterem** — bez kodowania — i dać mu zarządzanie
Twoimi kontami w kierunku mierzalnych celów. Agent to jak cBot napędzany osobowością: wybierasz archetyp
i nastawienie, ustawiasz zabezpieczenia i uruchamiasz go pod nadzorem **Jądra Autonomii i Bezpieczeństwa**.

Otwórz **AI → Agent Studio** (`/agent-studio`).

## Tworzenie agenta

Dialog **Nowy agent** zbiera, bez kodowania:

- **Nazwa** i **archetyp** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion lub Breakout/Momentum. Każdy preset ustala sensowny rhythm i postawę.
- **Nastawienie** — suwakami agresywności, cierpliwości i podążania za trendem.
- **Poziom autonomii** — **Doradczy** (tylko proponuje) lub **Z zatwierdzeniem** (działa dopiero po Twojej
  aprobacie każdego działania). **Pełna automatyka** (bez zgody na każdą transakcję) dodatkowo wymaga **zakonczenia ryzyka**
  i akceptacji disclaimer'u ryzyka przed uzbrojeniem.

Persona kompiluje się **deterministycznie** w systemowy prompt agenta (bez autora LLM), więc
ta sama konfiguracja zawsze daje identyczne instrukcje — odtwarzalne i audytowalne.

## Rejestr

Każdy agent pojawia się w tabeli sali kontroli: **który agent, jego typ, ile kont nim zarządza, jego
cele, status uruchomienia i ostatnie działanie**, z kontrolkami **Start / Stop / Kill**. Przycisk Kill wstrzymuje
uruchomionego agenta natychmiast.

## Bezpieczeństwo to invariant domeny, nie ustawienie

Wszystko dotyczące pieniędzy przechodzi przez **Jądro Autonomii i Bezpieczeństwa**:

- **Zakonczenie ryzyka** — twarde limity na zamówienie (max dzienna strata, otwarta ekspozycja, rozmiar pozycji, dźwignia,
  kolejne straty, zamówienia/godzina, dozwolone symbole). Każde zamówienie jest sprawdzane pod kątem tego limitu przed wysłaniem;
  naruszenie zostaje odrzucone, a nie ograniczone. Wymagane zanim agent może osiągnąć Pełną automatykę.
- **Wyłącznik** — deterministycznie wstrzymuje nowe ryzyko w przypadku serii strat, naruszenia dziennej straty, **twardego
  naruszenia celu wydajności** lub **niedostępności dostawcy AI** (model niedostępny lub halucynujący nigdy nie otwiera
  nowych pozycji).
- **Wersjonowane potwierdzenie disclaimer** — jednorazowa, wersjonowana akceptacja wymagana do uzbrojenia Pełnej automatyki
  (prawnie wymagana zgoda, nie zgoda na każdą transakcję); zmiana disclaimer'u wymusza ponowną zgodę.
- **Przycisk awaryjny** — idempotentny emergency halt na każdym uruchomionym agencie.

## Cele

Daj agentowi **mierzalne cele** — np. *utrzymuj max drawdown poniżej 4%*, *profit factor co najmniej
1.5*, *win rate ≥ 55%*. Każdy cel to **Twardy** (zabezpieczenie — naruszenie wyzwala wyłącznik) lub
**Miękki** (tylko steruje rozumowaniem), oceniany jako On-track / At-risk / Breached.

## Pipeline decyzji

Po uruchomieniu agent uruchamia **nadzorowaną pętlę 24/7** (`AgentRuntimeService`). Każdy tick, dla każdego
zarządzanego konta: czyta **deterministyczny stan konta** (prawdę, nigdy pamięć modelu);
pyta silnik decyzyjny o ruch; przekazuje go przez **bramę bezpieczeństwa** (`AgentDecisionProcessor`) —
poziom autonomii → wyłącznik → zakonczenie ryzyka; zapisuje append-only **`AgentDecisionRecord`**; i
wstrzymuje się lub wykonuje zgodnie z kierunkami bramy. Pętla jest **fault-isolated** (niepowodzenie jednego agenta nigdy
nie dotyka innych ani hosta) i **bezpieczna domyślnie**: jest inertna, chyba że AI jest skonfigurowany *i*
`App:Ai:AgentRuntimeEnabled` jest ustawiony, i nigdy nie otwiera nowego ryzyka, gdy dostawca AI jest niedostępny.

- **Brama zatwierdzenia** — proponowane zamówienie agenta **Z zatwierdzeniem** jest zapisywane jako **Oczekujące** i
  nic nie robi, dopóki właściciel go nie zatwierdzi (`POST /api/agent-studio/{id}/decisions/{seq}/approve` lub
  `/reject`); **Pełna automatyka** przechodzi przez konwert bez zatwierdzenia per-transakcję; **Doradczy** tylko
  proponuje.
- **Ledger audytu** — każda decyzja jest odtwarzalna: rozumowanie (XAI), przytaczane dowody, werdykt bramy,
  intencja zamówienia i czy się wykonało, na `GET /api/agent-studio/{id}/decisions`.
- **Research desk** — debata wieloagentowa na żądanie: analitycy Alpha/Sentiment/Technical/Risk każdy dają
  pogląd i Reviewer syntetyzuje propozycję (`POST /api/agent-studio/{id}/debate`).
- **Pamięć** — agent pamięta każdą decyzję i przywołuje niedawną pamięć do następnego prompta dla
  ciągłości (`GET /api/agent-studio/{id}/memory`).

Każdy wiersz rejestru **Szczegóły** otwiera feed decyzji agenta (z Approve/Reject na oczekujących zamówieniach),
jego pamięć i kartę Run-debate.

## Zakres

Wysłane: pełny cykl życia agenta, deterministyczna brama bezpieczeństwa, runtime 24/7, human-in-the-loop
brama zatwierdzenia, ledger audytu i **integracja na żywo z cTrader Open API** — magazyn stanu konta
(czyta rzeczywisty bilans, pozycje i otwartą ekspozycję w lotach) i executor zamówień (umieszcza rzeczywiste zlecenia rynkowe,
lot→volumen przez rozmiar lotu symbolu), oba rozpoznające poświadczenia OAuth każdego zarządzanego konta i
degradujące się bezpiecznie gdy konto nie jest połączone. **Wymaga klucza API Anthropic** aby model
generował zamówienia (dopóki silnik się wstrzymuje); jeszcze do przyjścia to role wieloagentowej debaty i warstwowa
pamięć/refleksja. Runtime jest wyłączony chyba że `App:Ai:AgentRuntimeEnabled` jest ustawiony, więc live trading
tylko zachodzi na explicit, w pełni wyraża, opt-in.
