---
slug: /intro
title: Üdvözöljük a cMind-ban
description: Barátságos bevezetés a cMind-ba — a cTrader nyílt forráskódú, saját szerveren futtatható kereskedési operációs platformja.
sidebar_position: 1
---

# Üdvözöljük a cMind-ban 👋

:::warning[Alfa szoftver — nem production-kész]
A cMind aktív fejlesztés alatt áll. Számítson egyenetlenségekre, verziók közötti kompatibilitástörő változásokra és még folyamatban lévő funkciókra. **Közösségi tesztelőkre, hibajelentőkre és korai közreműködőkre van szükségünk** a platform alakításához. Ha problémába ütközik, [jelezze](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) — a valós visszajelzése a legértékesebb dolog, amit most hozzájárulhat.
:::

Tehát kereskedési botokat szeretne építeni, backtesztelni anélkül, hogy megolvasztaná a laptopját,
több gépen futtatni, tranzakciókat tükrözni egy tucat számlára, és hagyni, hogy egy AI figyelje a
kockázatot alvás közben. **Pontosan a megfelelő helyen jár.**

A cMind egy **nyílt forráskódú, saját szerveren futtatható kereskedési operációs platform cTrader-hez**.
Képzelje el mint a teljes kereskedési asztalát — szerzés, végrehajtás, számítási flotta, copy trading
és egy AI mag — egyetlen nyugodt, sötét, mobilbarát alkalmazásba csomagolva, amelyet teljes egészében
Ön birtokol.

:::tip[Egy mondatban]
Építsen → backtesztelje → futtassa → másolja cTrader stratégiáit nagy léptékben, beépített AI-val,
saját szerverein, saját márkájával.
:::

## Mit tud valójában?

| Ön szeretné… | A cMind megteszi | Tudjon meg többet |
|---|---|---|
| cBot-ot írni a böngészőben | Monaco IDE + C#/Python sablonok, sandboxolt buildek | [Építés és backteszt](./features/build-and-backtest.md) |
| Több gépen backtesztelni | Öngyógyító csomópontflotta választja a legkevésbé terhelt gépet | [Skálázás](./deployment/scaling.md) |
| Egy számlát sokra másolni | Robusztus tükrözés újraszinkronizálással, dupla tranzakciók nélkül | [Copy trading](./features/copy-trading.md) |
| Az AI-ra bízni az unalmas munkát | Stratégiagenerálás, önjavítás, kockázati őrző, utóelemzés | [AI mag](./features/ai.md) |
| Prop firm szabályokon belül maradni | Élő tőkekövető + challenge szabályszimuláció | [Prop-firm](./features/prop-firm.md) |
| Backteszt-előnyt validálni | PSR / DSR / t-stat túlillesztés-korrekció | [Backtest Integrity Lab](./features/backtest-integrity.md) |
| Megérteni a saját szokásait | Viselkedési szivárgás-detektálás + AI edző | [Kereskedési napló](./features/trading-journal.md) |
| Makroesemények nyomon követése stratégiához | Pont-időbeli naptár, hírek blackout, cBot API | [Gazdasági naptár](./features/economic-calendar.md) |
| Deviza makroerejét értékelni | AI előretekintő kilátás minden párra | [Devizaerő](./features/currency-strength.md) |
| Számlák biztosítása 2FA-val | TOTP hitelesítő alkalmazás + biztonsági kódok | [Kétfaktoros hitelesítés](./features/two-factor-auth.md) |
| *Saját* termékként szállítani | Teljes fehér cimkézés: név, színek, logó, favicon | [Fehér cimkézés](./features/white-label.md) |
| A tulajdonosoknak futás közbeni hangolást engedni | Minden fehér cimkézési opció élőben a Beállítások → Telepítés menüben | [Tulajdonosi beállítások](./features/white-label-owner-settings.md) |
| Bármilyen nyelven futtatni | 23 nyelv, beleértve az RTL-t — a build hiányzó kulcsnál sikertelen | [Lokalizáció](./features/localization.md) |
| Telefonon futtatni | Telepíthető, mobilközpontú PWA | [PWA](./features/pwa.md) |
| AI kliensből vezérelni | Beépített MCP szerver (HTTP + SSE) | [MCP](./features/mcp.md) |

## Az 5 perces útvonal ⏱️

Ha van Dockerje és öt perce, most azonnal elkezdhet egy valódi cMind példányt kipróbálni:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Ezután nyissa meg a **<http://localhost:8080>** oldalt, jelentkezzen be, és készen áll. A teljes útmutató
(a Docker elkerülhetetlen véleményeinek elhárításával) megtalálható a
**[Helyi futtatás](./deployment/local.md)** oldalon.

## Újonc? Kövesse a sárgatégla-utat 🟡

1. **[Kinek szól?](./audience.md)** — győződjön meg, hogy a mi típusú problémánk.
2. **[Helyi futtatás](./deployment/local.md)** — indítson el egy valódi példányt.
3. **[Funkciók](./features/README.md)** — a tartalom teljes körű bemutatása.
4. **[Éles telepítés](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Tegye magáévá](./white-label-for-business.md)** — fehér cimkézze üzleti használatra.
6. **[Közreműközés](./contributing.md)** — a PR-ok (emberi *és* AI-asszisztált) nagyon szívesen látottak.

## Egy gyors szó a pénzről 💸

A cMind **valódi tőkét** mozgat. Ezt komolyan vesszük — minden változtatás egységtesztekkel,
integrációs és end-to-end tesztekkel érkezik, beleértve a hibás utakat is (megszakított kapcsolatok,
visszautasított megbízások, leállt csomópontok). Önnek is komolyan kell vennie: **tesztelje először
demószámlán**, és olvassa el a [megfelelőségi megjegyzéseket](./features/compliance.md), mielőtt
bármilyen éles dologra ráirányítaná. A kereskedés kockázatos; ez a szoftver egy eszköz, nem pénzügyi
tanács.

Rendben — elég a bevezetőből. Menjünk és építsünk valamit. →
