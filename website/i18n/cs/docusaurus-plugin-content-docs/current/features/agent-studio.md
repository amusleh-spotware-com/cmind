---
description: "Agent Studio — vytvářejte trading agenty řízené personou s charakterem a archetypem, kteří spravují účty k vašim cílům pod Autonomy & Safety Kernel (rizikový obál, jistič, killswitch, verzovaný souhlas s vyloučením odpovědnosti)."
---

# Agent Studio

Agent Studio vám umožňuje vytvořit **trading agenta s charakterem** — bez kódu — a svěřit mu správu vašich účtů k měřitelným cílům. Agent je jako personality-driven cBot: zvolíte archetyp a temperament, nastavíte ochranná pravidla a běží pod **Autonomy & Safety Kernel**.

Otevřete **AI → Agent Studio** (`/agent-studio`).

## Vytvořte agenta

Dialog **Nový agent** sbírá, bez kódu:

- **Název** a **archetyp** — Scalper, Denní obchodník, Swing obchodník, Poziční obchodník, News Trader, Contrarian, Mean Reversion nebo Breakout/Momentum. Každá předvolba nastavuje rozumnou kadenci a držení.
- **Temperament** — posuvníky pro agresivitu, trpělivost a sledování trendu.
- **Úroveň autonomie** — **Advisory** (pouze navrhuje) nebo **Approval-gated** (jedná pouze po vašem schválení každé akce). **Full Auto** (bez schválení každého obchodu) navíc vyžaduje **rizikový obál** a přijetí vyloučení odpovědnosti před aktivací.

Persona se **deterministicky** kompiluje do system promptu agenta (žádný LLM ji netvoří), takže stejná konfigurace vždy produkuje stejné instrukce — reprodukovatelné a auditovatelné.

## Seznam agentů

Každý agent se zobrazuje v přehledové tabulce: **který agent, jeho typ, kolik účtů spravuje, jeho cíle, stav běhu a poslední akce**, s ovládacími prvky **Start / Stop / Kill**. Killswitch zastaví běžícího agenta okamžitě.

## Bezpečnost je doménový invariant, ne nastavení

Vše, co se dotýká peněz, prochází přes **Autonomy & Safety Kernel**:

- **Rizikový obál** — pevné limity na objednávku (max denní ztráta, otevřená expozice, velikost pozice, páka, po sobě jdoucí ztráty, objednávky/hodina, povolené symboly). Každá objednávka je před odesláním proti němu ověřena; porušení je odmítnuto, ne omezeno. Vyžadováno předtím, než agent může dosáhnout Full Auto.
- **Jistič** — deterministicky zastaví nové riziko při sérii ztrát, porušení denního limitu ztráty, **porušení tvrdého výkonnostního cíle** nebo **nedostupnosti poskytovatele AI** (model, který je nedostupný nebo halucinuje, nikdy neotevře nové pozice).
- **Verzovaný souhlas s vyloučením odpovědnosti** — jednorázový, verzovaný souhlas je vyžadován pro aktivaci Full Auto (legálně požadovaný souhlas, nikoli schválení každého obchodu); změna vyloučení nutí k novému souhlasu.
- **Killswitch** — idempotentní nouzové zastavení každého běžícího agenta.

## Cíle

Dejte agentovi **měřitelné cíle** — např. *udržuj max drawdown pod 4%*, *profit factor alespoň 1.5*, *win rate ≥ 55%*. Každý cíl je **Hard** (ochranné pravidlo — porušení spustí jistič) nebo **Soft** (pouze řídí uvažování), vyhodnoceno jako On-track / At-risk / Breached.

## Rozhodovací pipeline

Jakmile je spuštěn, agent běží ve **24/7 supervizované smyčce** (`AgentRuntimeService`). Každý tik, pro každý spravovaný účet: čte **deterministický stav účtu** (základní pravda, nikdy paměť modelu); ptá se rozhodovacího engine na tah; předává to přes **bezpečnostní bránu** (`AgentDecisionProcessor`) — úroveň autonomie → jistič → rizikový obál; zapisuje append-only **`AgentDecisionRecord`**; a zastavuje se nebo provádí podle verdiktu brány. Smyčka je **izolovaná proti selháním** (selhání jednoho agenta se nedotkne jiného ani hostitele) a **bezpečná defaultně**: je nečinná, dokud není AI nakonfigurována *a* nastaveno `App:Ai:AgentRuntimeEnabled`, a nikdy neotevře nové riziko, když je poskytovatel AI nedostupný.

- **Schvalovací brána** — navržená objednávka **Approval-gated** agenta je zaznamenána jako **Pending** a nedělá nic, dokud ji owner neschválí (`POST /api/agent-studio/{id}/decisions/{seq}/approve` nebo `/reject`); **Full Auto** prochází obálem bez schválení každého obchodu; **Advisory** pouze navrhuje.
- **Auditní účetní kniha** — každé rozhodnutí je přehratelné: odůvodnění (XAI), důkazy, které citovalo, verdikt brány, záměr objednávky a zda byla provedena, na `GET /api/agent-studio/{id}/decisions`.
- **Výzkumní oddělení** — na požádání multi-agent debate: Alpha/Sentiment/Technical/Risk analytici každý dávají svůj pohled a Reviewer syntetizuje návrh (`POST /api/agent-studio/{id}/debate`).
- **Paměť** — agent si pamatuje každé rozhodnutí a vybavuje si nedávnou paměť do dalšího promptu pro kontinuitu (`GET /api/agent-studio/{id}/memory`).

**Details** každého řádku v seznamu otevírá decision feed agenta (s Approve/Reject na čekajících objednávkách), jeho paměť a záložku Run-debate.

## Rozsah

Odesláno: úplný životní cyklus agenta, deterministická bezpečnostní brána, 24/7 runtime, schvalovací brána s člověkem ve smyčce, auditní účetní kniha a **živá integrace cTrader Open API** — úložiště stavu účtu (čte skutečný zůstatek, pozice a otevřenou expozici v lotech) a exekutor objednávek (umisťuje skutečné market objednávky, lots→volume přes velikost lotu symbolu), obě řeší OAuth pověření každého spravovaného účtu a bezpečně degradovat, když účet není propojen. **Vyžaduje Anthropic API klíč** pro model, aby generoval objednávky (do té doby engine drží); teprve přijdou multi-agent debate role a vrstvená paměť/reflexe. Runtime je vypnut, dokud není nastaveno `App:Ai:AgentRuntimeEnabled`, takže živý trading se děje pouze na explicitní, plně odsouhlasený opt-in.

## Spravované účty a úpravy

Při vytváření agenta si vyberete obchodní účet(y), které spravuje (vyžadováno před jeho spuštěním). Každého agenta lze потом **upravit** (název, temperament, autonomii a spravované účty) z ikonky tužky na jeho řádku v seznamu. Ovládací prvky životního cyklu (details, edit, start, stop, kill) jsou ikonová tlačítka, každé zakázané ve stavech, kde akce neplatí.
