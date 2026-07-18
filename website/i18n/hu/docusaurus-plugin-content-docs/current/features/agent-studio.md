---
description: "Agent Studio — személyiség-vezérelt, kód nélküli kereskedési ügynököket hozz létre, amelyek az Autonómia és Biztonság Kernel alatt kezelik a fiókodat a célok felé (kockázati határok, áramkör-szakadó, vészleállítás, verziózott jogi felelősségvállalás)."
---

# Agent Studio

Az Agent Studio lehetővé teszi, hogy egy **személyiséggel rendelkező kereskedési ügynököt** hozz létre — kód nélkül — és adhass neki fiókvezérlést a mérhető célok felé. Az ügynök olyan, mint egy személyiség-vezérelt cBot: kiválasztasz egy archetípust és hozzáállást, beállítod az őrségeket, és az **Autonómia és Biztonság Kernel** alatt fut.

Nyisd meg az **AI → Agent Studio** (`/agent-studio`).

## Ügynök létrehozása

A **Új ügynök** dialog, kód nélkül, gyűjt:

- **Név** és **archetípus** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader, Contrarian, Mean Reversion vagy Breakout/Momentum. Minden előbeállítás egy értelmes ritmuslejtést és testtartást rögzít.
- **Hozzáállás** — agresszivitás, türelem és trend-követési csúszkák.
- **Kezelt fiók(ok)** — **legalább egy szükséges az ügynök létrehozásához** (egy fiókok nélküli ügynök soha nem tudna elindulni, így a *Létrehozás* letiltva marad, amíg nem választsz egyet). Ha még nem linkeltél egy kereskedési fiókot, a dialog azt jelzi, és rámutat, hogy előbb linkeld meg az egyiket.
- **Autonómia szint** — **Advisory** (csak javasol) vagy **Approval-gated** (csak az általad adott engedély után működik). **Full Auto** (per-trade engedély nélkül) ráadásul szükséges egy **kockázati határokat** és a kockázati nyilatkozat elfogadása az aktívvá válás előtt.

A persona **determinisztikusan** fordul le az ügynök rendszerpromptjévé (az LLM nem írja meg), így ugyanaz a konfiguráció mindig ugyanaz az utasítást eredményezi — reprodukálható és auditálható.

## A csapat

Minden ügynök egy vezérlőterem táblában jelenik meg: **melyik ügynök, típusa, hány fiókot kezel, céljai, futási állapota és utolsó művelete**, az **Indítás / Leállítás / Megöl** vezérlésekkel. A Megöl kapcsoló azonnal leállít egy futó ügynököt.

## A biztonság egy tartomány invariáns, nem beállítás

Minden pénzkezeléses útvonal az **Autonómia és Biztonság Kernel**-en keresztül:

- **Kockázati határok** — kemény megrendelésenként limitek (max napi veszteség, nyitott expozíció, pozíció mérete, tőkeáttétel, egymást követő veszteségek, megrendelések/óra, engedélyezett szimbólumok). Minden rendelés az elküldés előtt ellenőrzésre kerül; a megsértés elutasítva, nem korlátozott. Szükséges, mielőtt egy ügynök elérheti az Full Auto-t.
- **Áramkör-szakadó** — determinisztikusan leállítja az új kockázatot egy veszteségsorozaton, egy napi-veszteség megsértésen, egy **kemény teljesítménycél megsértésen**, vagy **AI-szolgáltató elérhetetlenségén** (egy halott vagy hallucináló modell soha nem nyit meg friss pozíciókat).
- **Verziózott jogi felelősségvállalás** — egy egyszeri, verziózott elfogadás szükséges az Full Auto aktívvá tételéhez (jogszabályi által megkövetelt beleegyezés, nem per-trade engedély); a nyilatkozat frissítése újbóli hozzájárulást kényszerít.
- **Megöl kapcsoló** — egy idempotens vészleállítás minden futó ügynökön.

## Célok

Adj az ügynöknek **mérhető célkitűzéseket** — pl. *tartsd a maximális drawdownt 4% alatt*, *profit faktor legalább 1,5*, *nyerési arány ≥ 55%*. Minden cél **Kemény** (egy őrség — a megsértés kiváltja az áramkör-szakadót) vagy **Puha** (csak az érvelést vezérli), értékelve On-track / At-risk / Breached-ként.

## A döntési csatorna

Az indítás után az ügynök egy **24/7 felügyelt hurkot** futtat (`AgentRuntimeService`). Minden alkalommal, minden kezelt fióknál: beolvassa a **determinisztikus fiók állapotát** (alapigazság, soha a modell emlékezete); megkérdezi a döntési motort egy mozgásról; átmegy a **biztonsági kapun** (`AgentDecisionProcessor`) — autonómia szint → áramkör-szakadó → kockázati határok; ír egy csak hozzáfűző **`AgentDecisionRecord`**-ot; és leállít vagy végrehajt, amint azt a kapu irányítja. A hurok **hiba-izolált** (egy ügynök meghibásodása soha nem érinti mást vagy a gazdagépet) és **alapértelmezés szerint biztonságos**: inert, hacsak az AI nincs konfigurálva *és* az `App:Ai:AgentRuntimeEnabled` beállítva van, és soha nem nyit meg friss kockázatot, míg az AI-szolgáltató elérhetetlenül.

- **Jóváhagyás kapu** — egy **Approval-gated** ügynök javasolt rendelése **Függőben** kerül rögzítésre és semmit nem csinál, amíg a tulajdonos jóvá nem hagyja (`POST /api/agent-studio/{id}/decisions/{seq}/approve` vagy `/reject`); **Full Auto** átmegy az őrségen per-trade engedély nélkül; **Advisory** csak javasol.
- **Audit főkönyv** — minden döntés visszajátszható: érvelés (XAI), az idézett bizonyítékok, a kapu ítélete, a rendelés szándéka és hogy végrehajt-e, `GET /api/agent-studio/{id}/decisions`-nél.
- **Kutatási asztal** — egy igény szerinti multi-ügynök vita: Alpha/Sentiment/Technical/Risk elemzők mindegyike egy nézetet ad és egy Felülvizsgáló szintetizál egy javaslatot (`POST /api/agent-studio/{id}/debate`).
- **Memória** — az ügynök megjegyez minden döntést és vissza tudja idézni közelmúltbeli memóriát a következő promptjébe az folytonosságért (`GET /api/agent-studio/{id}/memory`).

Minden csapat sorának **Részletei** megnyitja az ügynök döntési csatornáját (jóváhagyás/elutasítás függőben lévő rendeléseken), emlékezetét és egy Vita futtatás fület.

## Terjedelem

Szállítva: az ügynök teljes életciklusa, a determinisztikus biztonsági kapu, a 24/7 futásidő, az emberi visszacsatolás jóváhagyási kapu, az audit főkönyv, és az **élő cTrader Open API integráció** — a fiók-állapot áruház (valódi egyenleg, pozíciók és nyitott expozíció sokszoros olvasása) és a rendelés-végrehajt (valódi piaci rendeléseket helyez, sok→volumen a szimbólum sokfüggvénye révén), mindkettő az egyes kezelt fiók OAuth hitelesítő adatait oldja fel és biztonságosan degradál, amikor egy fiók nincs linkeltve. **Az Anthropic API kulcs szükséges** a rendeléseket generáló modellhez (addig az motor tartja); még jönnek a multi-ügynök vita szerepei és rétegzett memória/reflexió. A futásidő ki van kapcsolva, hacsak az `App:Ai:AgentRuntimeEnabled` be nincs állítva, így az élő kereskedés csak egy explicit, teljes-beleegyezésre utaló opt-in után történik.

## Kezelt fiókok és szerkesztés

Az ügynök létrehozásakor kiválasztod a kereskedési fióko(ka)t, amit kezel — **legalább egy szükséges a létrehozáskor** (a *Létrehozás* gomb letiltva marad, amíg nem választasz egyet, és a create végpont elutasít egy üres kiválasztást). Minden ügynök **szerkeszthető** utána (név, temperamentum, autonómia, és kezelt fiókok) a ceruza ikonból a csapat során. Életciklus-vezérlések (részletetek, szerkesztés, indítás, leállítás, megöl) ikon gombok, mindegyik letiltva az állapotokban, ahol a művelet nem alkalmas.
