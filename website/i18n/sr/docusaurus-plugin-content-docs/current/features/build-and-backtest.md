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
