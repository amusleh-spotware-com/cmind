---
description: "Gradnja, pokretanje, testiranje unatrag cTrader cBot-ova (C# i Python, oba .NET) iz in-browser Monaco IDE, pokrenuti na zvaničnoj ghcr.io/spotware/ctrader-console slici."
---

# Gradnja i testiranje unatrag cBot-ova

Gradnja, pokretanje, testiranje unatrag cTrader cBot-ova (C# **i** Python, oba .NET) iz in-browser Monaco
IDE, pokrenuti na zvaničnoj `ghcr.io/spotware/ctrader-console` slici.

## Gradnja

- Stranica **Builder** hostuje Monaco editor; `CBotBuilder` kompajlira projekat sa
  `dotnet build` **u jednokratnom kontejneru** (`AppOptions.BuildImage`, work dir bind-mount
  na `/work`), tako da nekomercijalni MSBuild target-i korisnika ne mogu do host sistema. NuGet restore keširan
  preko build-ova putem shared volume. Web host mora imati pristup Docker socket-u.
- C# + Python starter šabloni se nalaze u `src/Nodes/Builder/Templates/`.

## Pokretanje i testiranje unatrag

- **Instance** = TPH hijerarhija stanja (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Tranzicija zamenjuje entitet (promena id-ja),
  id kontejnera se prenosi.
- `NodeScheduler` bira najmanje opterećen čvor; `ContainerDispatcherFactory` rutira na
  remote node HTTP agenta ili lokalni Docker dispatcher.
- Completion pollers usklađuju izašle kontejnere (backtest kontejneri se sami gasne preko
  `--exit-on-stop`); izveštaj prisutan → završen (čuva `ReportJson`), nedostaje → neuspeo.
- Live logovi kontejnera se streamuju u browser preko SignalR-a; equity krive iz backtest-a se parsiraju iz
  izveštaja i prikazuju na grafikonu.

## Napomene za cTrader Console CLI

Backtest-i zahtevaju `--data-mode` (podrazumevano `m1`), datume kao `dd/MM/yyyy HH:mm`, i
`params.cbotset` JSON positional arg; `run` odbacuje `--data-dir` (samo backtest). Pogledajte
`ContainerCommandHelpers`.

## Čvorovi i skaliranje

Kapacitet izvršenja se skalira dodavanjem node agenata (samo-registracija + heartbeat). Pogledajte
[otkrivanje čvoreva](../operations/node-discovery.md) i [skaliranje](../deployment/scaling.md).

## Pokretanje iz editora koda

Klik na **Pokreni** u editoru koda otvara dijalog umesto slepog, čvrsto kodiranog pokretanja:

- **Trgovački nalog** (obavezno) — cTrader nalog na koji se cBot povezuje.
- **Skup parametara** (opciono) — izaberite postojeći skup ili ostavite prazno da biste pokrenuli sa **podrazumevanim vrednostima parametara** cBota. Dugme **+** pored birača kreira novi skup parametara na licu mesta (vidite ispod) i bira ga.
- **Simbol / Vremenski okvir** su podrazumevano `EURUSD` / `h1` i mogu se promeniti; **Otkaži** ili **Pokreni**.

Pri **Pokreni**, editor čuva i gradi trenutni izvorni kod, pokreće instancu na izabranom nalogu sa izabranim parametrima, a zatim prati žive logove kontejnera. (Tok logova prosleđuje autentifikacioni kolačić prijavljenog korisnika SignalR hab-u `/hubs/logs`, pa se povezuje umesto da otkaže sa `Invalid negotiation response received`.)

## Skupovi parametara

**Skup parametara** je imenovan, ponovo upotrebljiv skup zamena parametara cBota, sačuvan kao ravan JSON objekat koji svako ime parametra mapira na skalarnu vrednost, npr. `{"Period": 14, "Label": "trend"}`. U trenutku pokretanja/backtesta pretvara se u cTrader datoteku `params.cbotset` (`{ "Parameters": { … } }`). Skup možete kreirati/urediti kao sirovi JSON iz dijaloga **Skupovi parametara** cBota ili na licu mesta iz dijaloga Pokreni.

JSON se pri čuvanju **validira**: mora biti jedan ravan objekat čije su sve vrednosti skalarne (string / broj / bool). Koren koji nije objekat, niz, ugnežđeni objekat, `null` vrednost ili neispravan JSON se odbacuje (jasna greška u dijalogu, `400 Bad Request` na API-ju). Prazan objekat `{}` je dozvoljen i znači „bez zamena".

## Kontrole životnog ciklusa instance

Svaki red instance (i njena stranica s detaljima) ima kontrole u skladu sa stanjem. **Aktivna** instanca prikazuje **Zaustavi**; **terminalna** (Zaustavljena / Završena / Neuspela) prikazuje **Pokreni (▶)** za ponovno pokretanje sa istim cBotom, nalogom, simbolom, vremenskim okvirom, skupom parametara i slikom (pokretanje se ponovo pokreće kao pokretanje, backtest kao backtest). Klik na Zaustavi prikazuje obaveštenje „Zaustavljanje…" i onemogućava ikonicu dok se ne završi; novokreirano pokretanje se odmah pojavljuje na listi — bez ponovnog učitavanja stranice.

Logovi konzole se **čuvaju kada se instanca završi** — kako za pokretanje (pri zaustavljanju), tako i za **backtest** (pri završetku) — pa logovi poslednjeg pokretanja ostaju vidljivi na stranici s detaljima i preuzimljivi preko ikonice **Preuzmi logove** čak i nakon što kontejner nestane.
