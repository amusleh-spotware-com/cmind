---
description: "Agent Studio — vytváření personalizovaných obchodních agentů bez kódu s charakterem a archetypem, který spravují účty směrem k vašim cílům pod Autonomy & Safety Kernel (obálka rizika, pojistka, kill switch, verzovaný souhlas s prohlášením o odpovědnosti)."
---

# Agent Studio

Agent Studio vám umožňuje vytvořit **obchodního agenta s charakterem** — bez kódu — a dát mu správu vašich účtů směrem k měřitelným cílům. Agent je jako osobností řízený cBot: zvolíte archetype a postoj, nastavíte zábrany a běží pod **Autonomy & Safety Kernel**.

Otevřete **AI → Agent Studio** (`/agent-studio`).

## Vytvoření agenta

Dialog **Nový agent** shromažďuje bez kódu:

- **Jméno** a **archetype** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader, Contrarian, Mean Reversion nebo Breakout/Momentum. Každá předvolba nastaví rozumný takt a postoj.
- **Postoj** — posuvníky agresivity, trpělivosti a sledování trendů.
- **Úroveň autonomie** — **Advisory** (pouze navrhuje) nebo **Approval-gated** (jedná pouze po vaší schválení per-action). **Full Auto** (bez schválení na obchod) navíc vyžaduje **obálku rizika** a přijetí zřeknutí se odpovědnosti před aktivací.

Persona se **deterministicky** kompiluje do systémového promptu agenta (žádný LLM ji nesepisuje), takže stejná konfigurace vždy produkuje stejné pokyny — reprodukovatelné a auditovatelné.

## Seznam

Každý agent se zobrazí v tabulce řídící místnosti: **který agent, jeho typ, kolik účtů spravuje, jeho cíle, stav běhu a poslední akce**, s ovládacími prvky **Start / Stop / Kill**. Kill switch okamžitě zastaví běžícího agenta.

## Bezpečnost je doménová invarianta, ne nastavení

Vše, co se dotýká peněz, prochází **Autonomy & Safety Kernel**:

- **Obálka rizika** — tvrdé limity per-obchod (maximální denní ztráta, otevřená expozice, velikost pozice, páka, po sobě jdoucí ztráty, obchody/hodina, povolené symboly). Každý obchod se před odesláním ověří proti němu; porušení je odmítnuto, ne stlačeno. Vyžadováno před tím, než agent dosáhne Full Auto.
- **Pojistka** — deterministicky zastaví nové riziko na řetězci ztrát, porušení denní ztráty, **tvrdém porušení cíle výkonu** nebo **nedostupnosti poskytovatele AI** (model, který je vypnutý nebo halucinuje, nikdy neotevře nové pozice).
- **Verzovaný souhlas s prohlášením** — jednorázové verzované přijetí se vyžaduje k aktivaci Full Auto (právně požadovaný souhlas, ne schválení na obchod); zvýšení prohlášení vynutí opětovný souhlas.
- **Kill switch** — idempotentní nouzová zastávka na každém běžícím agentovi.

## Cíle

Dejte agentovi **měřitelné cíle** — např. *udržovat maximální drawdown pod 4%*, *faktor zisku alespoň 1,5*, *win rate ≥ 55%*. Každý cíl je **Hard** (zábrany — porušení spustí pojistku) nebo **Soft** (pouze řídí uvažování), vyhodnoceno jako On-track / At-risk / Breached.

## Pipeline rozhodnutí

Po spuštění agent běží v **24/7 supervizované smyčce** (`AgentRuntimeService`). Každý tick pro každý spravovaný účet: přečte si **deterministický stav účtu** (základní pravdu, nikdy paměť modelu); požádá rozhodovací engine o tah; projde to **bezpečnostní bránou** (`AgentDecisionProcessor`) — úroveň autonomie → pojistka → obálka rizika; zapíše si append-only **`AgentDecisionRecord`**; a zastaví se nebo se provede podle toho, jak brána řídí. Smyčka je **fault-isolated** (selhání jednoho agenta se nikdy nedotýká druhého nebo hostitele) a **bezpečná ve výchozím nastavení**: je inertní, pokud AI není nakonfigurena *a* `App:Ai:AgentRuntimeEnabled` není nastaven, a nikdy neotevře nové riziko, když je poskytovatel AI nedostupný.

- **Brána schválení** — navrhovaný obchod **Approval-gated** agenta se zaznamenává jako **Pending** a nic nedělá, dokud jej vlastník neschválí (`POST /api/agent-studio/{id}/decisions/{seq}/approve` nebo `/reject`); **Full Auto** prochází obálkou bez schválení na obchod; **Advisory** pouze navrhuje.
- **Kniha auditů** — každé rozhodnutí je přehrávatelné: uvažování (XAI), důkazy, které citovalo, verdikt brány, záměr obchodu a zda se provádel, na `GET /api/agent-studio/{id}/decisions`.
- **Research desk** — debata na vyžádání s více agenty: analytici Alpha/Sentiment/Technical/Risk každý poskytují názor a Reviewer syntézuje návrh (`POST /api/agent-studio/{id}/debate`).
- **Paměť** — agent si pamatuje každé rozhodnutí a vyvolá nedávnou paměť do svého dalšího promptu pro kontinuitu (`GET /api/agent-studio/{id}/memory`).

Řádek seznamu **Details** otevírá kanál rozhodnutí agenta (se schválením/odmítnutím u nevyřešených obchodů), jeho paměť a kartu Run-debate.

## Rozsah

Dodáno: plný životní cyklus agenta, deterministická bezpečnostní brána, 24/7 runtime, lidský zásah do schválení brány, kniha auditů a **integrace živého cTrader Open API** — úložiště stavu účtu (čte skutečný zůstatek, pozice a otevřenou expozici v lotech) a vykonavatel obchodu (umisťuje skutečné obchody na trhu, loty→objem přes velikost lotu symbolu), oba řeší OAuth pověření každého spravovaného účtu a bezpečně se degradují, pokud účet není propojen. **Vyžaduje klíč Anthropic API** pro model, aby generoval obchody (do té doby engine zadržuje); stále zbývají role debaty s více agenty a vrstvená paměť/reflexe. Runtime je vypnut, pokud není nastaven `App:Ai:AgentRuntimeEnabled`, takže živý obchodován se děje pouze na explicitní, plně přijatý opt-in.
