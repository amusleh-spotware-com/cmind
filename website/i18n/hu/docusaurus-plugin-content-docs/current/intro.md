---
slug: /intro
title: Üdvözöljük a cMindben
description: Barátságos bevezető a cMindbe — a cTraderhez készült nyílt forráskódú, önállóan üzemeltethető kereskedési műveleti platformba.
sidebar_position: 1
---

# Üdvözöljük a cMindben 👋

Tehát kereskedési botokat szeretne építeni, visszatesztelni őket anélkül, hogy megolvasztaná a laptopját,
több gépen futtatni, tucatnyi számlára tükrözni az ügyleteket, és hagyni, hogy egy MI figyelje a
kockázatot, míg Ön alszik. **Pontosan jó helyen jár.**

A cMind egy **nyílt forráskódú, önállóan üzemeltethető kereskedési műveleti platform a cTraderhez**.
Gondoljon rá úgy, mint a teljes kereskedési asztalára — szerkesztés, végrehajtás, számítási flotta,
másolásos kereskedés és egy MI-mag — egyetlen nyugodt, sötét, mobilbarát alkalmazásba csomagolva, amely
elejétől a végéig az Öné.

:::tip Egy mondatban
Építsen → backteszteljen → futtasson → másoljon cTrader stratégiákat méretben, beépített MI-vel, saját
szerverein és saját márkája alatt.
:::

## Mit tud valójában?

| Ön szeretné… | A cMind megteszi | Tudjon meg többet |
|---|---|---|
| cBotot írni a böngészőben | Monaco IDE + C#/Python sablonok, homokozós buildek | [Építés és backteszt](./features/build-and-backtest.md) |
| Gépek között backtesztelni | Egy önjavító csomópont-flotta a legkevésbé terhelt gépet választja | [Skálázás](./deployment/scaling.md) |
| Egy számlát sokra másolni | Robusztus tükrözés újraszinkronizálással, dupla ügyletek nélkül | [Másolásos kereskedés](./features/copy-trading.md) |
| Hagyni, hogy az MI végezze a favágást | Stratégiagenerálás, önjavítás, kockázatőr, utólagos elemzések | [MI-mag](./features/ai.md) |
| A prop cég szabályain belül maradni | Élő tőkekövetés + kihívásszabály-szimuláció | [Prop cég](./features/prop-firm.md) |
| *Az Ön* termékeként kiadni | Teljes white-label: név, színek, logó, favicon | [White-label](./features/white-label.md) |
| A telefonján futtatni | Telepíthető, mobil-első PWA | [PWA](./features/pwa.md) |
| MI-kliensről vezérelni | Beépített MCP-kiszolgáló (HTTP + SSE) | [MCP](./features/mcp.md) |

## Az 5 perces út ⏱️

Ha van Dockere és öt perce, máris egy valódi cMind-példányt piszkálhat:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Ezután nyissa meg a **<http://localhost:8080>** címet, jelentkezzen be, és már mehet is. A teljes
végigvezetés (hibaelhárítással arra az esetre, ha a Dockernek elkerülhetetlenül lesz véleménye) a
**[Helyi futtatás](./deployment/local.md)** oldalon található.

## Új itt? Kövesse a sárga téglás utat 🟡

1. **[Kinek szól ez?](./audience.md)** — győződjön meg róla, hogy a mi fajta bajunk.
2. **[Helyi futtatás](./deployment/local.md)** — indítson el egy valódi példányt.
3. **[Funkciók](./features/README.md)** — a teljes túra arról, mi van belül.
4. **[Éles telepítés](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Tegye a magáévá](./white-label-for-business.md)** — alkalmazzon white-labelt a vállalkozásához.
6. **[Járuljon hozzá](./contributing.md)** — a PR-ek (emberi *és* MI-segített) nagyon szívesen látottak.

## Néhány gyors szó a pénzről 💸

A cMind **valódi tőkét** mozgat. Komolyan vesszük — minden változás egység-, integrációs és
végpontok-közti tesztekkel érkezik, a hibautakat is beleértve (megszakadt kapcsolatok, elutasított
megbízások, halott csomópontok). Önnek is komolyan kell vennie: **először demószámlán teszteljen**, és
olvassa el a [megfelelőségi megjegyzéseket](./features/compliance.md), mielőtt bármi valósra irányítaná.
A kereskedés kockázatos; ez a szoftver eszköz, nem pénzügyi tanácsadás.

Rendben — elég a bevezetőből. Menjünk, építsünk valamit. →
