---
description: "Agent Studio — vytváranie obchodných agentov riadených personou bez kódu s charakterom a archetypom, ktorí spravujú účty smerom k vašim cieľom v rámci Autonomy & Safety Kernel (bezpečnostná obálka, istič, kill switch, verzovaný súhlas s vylúčením zodpovednosti)."
---

# Agent Studio

Agent Studio vám umožňuje vytvoriť **obchodného agenta s charakterom** — bez kódu — a zveriť mu správu vašich účtov smerom k merateľným cieľom. Agent je ako osobnostný cBot: vyberiete archetyp a postoj, nastavíte zábrany a agent beží v rámci **Autonomy & Safety Kernel**.

Otvorte **AI → Agent Studio** (`/agent-studio`).

## Vytvorenie agenta

Dialóg **Nový agent** zhromažďuje, bez kódu:

- **Názov** a **archetype** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion alebo Breakout/Momentum. Každý predvolený nastavenie fixuje rozumný rytmus a postoj.
- **Postoj** — agresivita, trpezlivosť a posuvníky trend-followingu.
- **Úroveň autonómie** — **Advisory** (navrhuje iba) alebo **Approval-gated** (koná iba po vašom
  schválení za každú akciu). **Full Auto** (bez schvaľovania za každý obchod) navyše vyžaduje **bezpečnostnú obálku**
  a prijatie vylúčenia zodpovednosti pred tým, ako sa môže ozbrojiť.

Persona sa kompiluje **deterministicky** do systémového promptu agenta (žiadny LLM ju nepíše), takže
rovnaká konfigurácia vždy produkuje rovnaké inštrukcie — reprodukovateľné a auditeľné.

## Súpis

Každý agent sa zobrazuje v tabuľke riadiacej miestnosti: **ktorý agent, jeho typ, koľko účtov spravuje, jeho
ciele, stav behu a posledná akcia**, s ovládacími prvkami **Štart / Stop / Kill**. Kill switch zastaví
bežiaci agent okamžite.

## Bezpečnosť je doménový invariant, nie nastavenie

Všetko, čo sa dotýka peňazí, prechádza cez **Autonomy & Safety Kernel**:

- **Bezpečnostná obálka** — tvrdé limity za objednávku (max denná strata, otvorená expozícia, veľkosť pozície, páka,
  po sebe nasledujúce straty, objednávky/hod, povolené symboly). Každá objednávka je voči nej validovaná pred odoslaním;
  porušenie je odmietnuté, nie skorigované. Vyžaduje sa predtým, než agent môže dosiahnuť Full Auto.
- **Istič** — deterministicky zastaví nové riziko pri sérii strát, dennom limite straty, **tvrdom
  porušení výkonnostného cieľa** alebo **nedostupnosti AI poskytovateľa** (model, ktorý je dole alebo halucinuje,
  nikdy neotvára nové pozície).
- **Verziovaný súhlas s vylúčením zodpovednosti** — jednorazový, verzovaný súhlas je potrebné prijať na ozbrojenie Full Auto
  (zákonom vyžadovaný súhlas, nie schválenie za každý obchod); zvýšenie verzie vylúčenia núti znovu-súhlas.
- **Kill switch** — idempotentné núdzové zastavenie na každom bežiacom agentovi.

## Ciele

Priraďte agentovi **merateľné ciele** — napr. *udržiavať max drawdown pod 4%*, *profit factor aspoň
1.5*, *win rate ≥ 55%*. Každý cieľ je **Hard** (poistka — porušenie spustí istič) alebo
**Soft** (ovplyvňuje iba uvažovanie), vyhodnotené ako On-track / At-risk / Breached.

## Rozhodovaci pipeline

Po spustení agent beží **24/7 supervizovanú slučku** (`AgentRuntimeService`). Každý tik, pre každý
spravovaný účet: číta **deterministický stav účtu** (základná pravda, nikdy pamäť modelu);
žiada rozhodovací engine o ťah; posiela to cez **bezpečnostnú bránu** (`AgentDecisionProcessor`) —
úroveň autonómie → istič → bezpečnostná obálka; zapisuje append-only **`AgentDecisionRecord`**; a
zastaví sa alebo vykoná podľa toho, čo brána nariadi. Slučka je **poruchovo izolovaná** (zlyhanie jedného agenta sa nikdy nedotkne
iného ani hostiteľa) a **bezpečná predvolene**: je nečinná, pokiaľ AI nie je nakonfigurovaná *a*
`App:Ai:AgentRuntimeEnabled` nie je nastavené, a nikdy neotvára nové riziko, kým je AI poskytovateľ nedostupný.

- **Brána schválenia** — navrhovaná objednávka **Approval-gated** agenta sa zaznamená ako **Pending** a
  neurobí nič, kým ju owner neschváli (`POST /api/agent-studio/{id}/decisions/{seq}/approve` alebo
  `/reject`); **Full Auto** prechádza cez obálku bez schvaľovania za každý obchod; **Advisory** iba
  navrhuje.
- **Audit ledger** — každé rozhodnutie je reprodukovateľné: odôvodnenie (XAI), dôkazy, ktoré citovalo,
  verdikt brány, zámer objednávky a či sa vykonala, na `GET /api/agent-studio/{id}/decisions`.
- **Research desk** — on-demand multi-agent debata: Alpha/Sentiment/Technical/Risk analytici každý dajú
  svoj pohľad a Reviewer syntetizuje návrh (`POST /api/agent-studio/{id}/debate`).
- **Pamäť** — agent si pamätá každé rozhodnutie a vťahuje nedávnu pamäť do ďalšieho promptu pre
  kontinuitu (`GET /api/agent-studio/{id}/memory`).

Každý riadok súpisu **Details** otvorí feed rozhodnutí agenta (s Approve/Reject na čakajúcich objednávkach),
jeho pamäť a záložku Run-debate.

## Rozsah

Dodané: úplný životný cyklus agenta, deterministická bezpečnostná brána, 24/7 runtime,
human-in-the-loop approval gate, audit ledger a **live cTrader Open API integrácia** — úložisko stavu účtu
(číta skutočný zostatok, pozície a otvorenú expozíciu v lotoch) a vykonávateľ objednávok (umiestňuje skutočné market
objednávky, lots→volume cez symbol lot size), oba resolvujú OAuth creds každého spravovaného účtu a
gracefully degradujú, keď účet nie je prepojený. **Vyžaduje Anthropic API kľúč** pre model na
generovanie objednávok (do vtedy engine drží); ešte prísť sú multi-agent debate role a layered
pamäť/reflexia. Runtime je vypnutý, pokiaľ nie je nastavené `App:Ai:AgentRuntimeEnabled`, takže živé obchodovanie sa len
deje na explicitný, plne-súhlasný opt-in.
