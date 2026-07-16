---
description: "Izgradi, pokreni, testira sa istorijom cTrader cBots (C# i Python, oba .NET) iz ugrađenog Monaco IDE-a, pokreće se na zvaničnoj ghcr.io/spotware/ctrader-console slici."
---

# Izgradi i testira cBots

Izgradi, pokreni, testira sa istorijom cTrader cBots (C# **i** Python, oba .NET) iz ugrađenog Monaco IDE-a, pokreće se na zvaničnoj `ghcr.io/spotware/ctrader-console` slici.

## Izgradnja

- Stranica **Builder** hostira Monaco editor; `CBotBuilder` kompajlira projekat sa `dotnet build` **u privremenom kontejneru** (`AppOptions.BuildImage`, radni direktorijum bind-montiran na `/work`), tako da nepouzdan korisnikov MSBuild ne dostiže domaćina. NuGet restauracija je keširana između izgradnji preko deljene zapremine. Veb domaćin treba pristup Docker soketu.
- C# i Python starter šabloni se nalaze u `src/Nodes/Builder/Templates/`.

## Pokreni i testira sa istorijom

- **Instances** = TPH hijerarhija stanja (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Prelazak zameni entitet (promena ID-a), container ID se prenosi.
- `NodeScheduler` bira najmanje opterećeni dozvoljeni čvor; `ContainerDispatcherFactory` rutira na HTTP agent udaljenog čvora ili lokalni Docker dispatcher.
- Polleři završetka usklađuju izašle kontejnere (backtest kontejneri se sami izlaze preko `--exit-on-stop`); izveštaj prisutan → završen (čuva `ReportJson`), nedostaje → neuspešan.
- Živi container logovi se streame u preglednik preko SignalR; backtest krive kapitala se raščlanjuju iz izveštaja i grafikuju.

## Backtest tržišni podaci su keširani po računu

cTrader Console preuzima istorijske tick/bar podatke u svoj `--data-dir`. Taj direktorijum je **stabilna, trajna keš ključana na trading računu** (njegov broj računa) — bind-montiran sa diska čvora na njegovoj putanji kontejnera (`/mnt/data`), **odvojena, ne-ugniježđena montaža** od direktorijuma rada po instanci. Dakle, svaki backtest na istom računu **ponovno koristi** već preuzete podatke umesto ponovnog preuzimanja. (Ranije je data dir živeo pod direktorijumom rada po instanci, čiji se ID menja sa svakim pokretanjem, što je forsiralo svež preuzimanje svakog backtesta.) Efemeralni direktorijum rada po instanci i dalje drži algoritam, parametre, lozinku i izveštaj; deljeni data keš se računa u upotrebi backtest-data čvora i briše akcijom čišćenja čvora.

## Postavke backtesta

**Backtest** dijalog izlaže korisnikove podložne postavke backtesta cTrader Console-a, tako da nikada ne morate da dodirnete komandnu liniju:

- **Symbol / Timeframe** — vremenski okvir je **padajući meni svakog cTrader perioda** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, i Renko/Range/Heikin periodi), u kanoničkom formatiranju konzole, tako da uvek birate važi `--period`.
- **From / To** — prozor backtesta (`--start` / `--end`).
- **Data mode** — jedan od tri cTrader moda (`--data-mode`): **Tick data** (`tick`, tačan), **m1 bars** (`m1`, brz), ili **Open prices only** (`open`, najbrži).
- **Starting balance** — podrazumevano `10000` (`--balance`). A **0 bilans ne plasira nikakve poslove i čini cTrader da emituje prazan izveštaj koji zatim pada na rušenju** ("Message expected"), tako da se uvek šalje nenula bilans.
- **Commission** i **Spread** — `--commission` / `--spread` (spread u pipima).

Direktorijum podataka (`--data-file` / `--data-dir`) upravlja sam aplikacija (keš po računu, pogledaj gore), nije izložen u dijalogu.

## Stranica detalja instance

Otvaranjem instance (`/instance/{id}`) prikazuje se njen živi status, logovi i — za backtest — kriva kapitala. **Naslov kartice pregledača** odražava specifičnu instancu (**ime cBot-a · vrsta · simbol**, npr. `TrendBot · Backtest · EURUSD`), tako da se kartica pokretanja i kartica backtesta mogu razlikovati na prvi pogled. Pokretanje i backtest istog cBot-a se prate kao zasebne **linije** (stabilan ID linije prenesen kroz prelaze stanja), tako da stranica prati tačno jednu instancu i nikada ne meša podatke pokretanja sa backtestom.

## Kontrole životnog ciklusa instance

Svaki red instance (i njegova stranica detalja) ima kontrole ispravne stanja. **Aktivna** instanca prikazuje **Stop**; **terminalna** (Stopped / Completed / Failed) prikazuje **Start (▶)** za ponovno pokretanje sa istim cBot-om, računom, simbolom, vremenskim okvirom, skupom parametara i slikom (pokretanje se restartuje kao pokretanje, backtest kao backtest). Klikom na Stop prikazuje se obaveštenje "Stopping…" i onemogućava ikonu dok se ne razreši, i novo kreirano pokretanje se pojavljuje na listi odmah — bez osvežavanja stranice.

Logovi konzole su **održavani kada se instanca završi** — za pokretanje (na Stop) i za **backtest** (pri završetku) — tako da logovi poslednjeg pokretanja ostaju vidljivi na stranici detalja i, preko alatne trake logovanja, **kopirani u privremenu memoriju** (Kopuj logs ikona) ili **preuzeti** (Preuzmi logs ikona) čak i nakon što je kontejner otišao. Oba deluju na punu logovanje konzole instance, a ne samo na vidljivi rep.

**Otpremljeni** `.algo` nikada nije izgrađen ovde, tako da njegov **Last Build** kolona na stranici cBots je ostavljena prazna (prikazuje vreme izgradnje samo za cBots koje gradite u pregledniku).

## Uredi i ponovno pokreni zaustavljenu instancu

**Zaustavljena** instanca (pokretanje ili backtest) ima kontrolu **Edit** — ikona na njenom redu na listi **i** pored Start/Stop-a na njenoj stranici detalja — koja otvara dijalog **preispunjen** sa njenom trenutnom konfiguracijom. Možete promeniti **trading račun, simbol, vremenski okvir, skup parametara i oznaku slike** (i, za backtest, **prozor i sve postavke backtesta** gore), zatim **Save & start** ga ponovno pokreće sa novim postavkama (zamenjujući zaustavljenu instancu). Kontrola je **onemogućena dok je instanca aktivna** — samo zaustavljena instanca može biti uređena.

## Pokreni iz koda editora

Klikom na **Run** u editoru koda otvara se dijalog umesto aktiviranja slepe, hard-code pokretanja:

- **Trading account** (obavezno) — cTrader račun kojem se cBot povezuje.
- **Parameter set** (opciono) — odaberi postojeći skup, ili ga ostavi prazan da pokrenete sa cBot-ovim **podrazumevanim vrednostima parametara**. Dugme **+** pored selektora kreira novi skup parametara u letnjem (pogledaj dole) i selektuje ga.
- **Symbol / Timeframe** podrazumevano su `EURUSD` / `h1` i mogu biti promenjeni; **Cancel** ili **Run**.

Na **Run** editor čuva + gradi trenutni izvor, pokreće instancu na odabranom računu sa odabranim parametrima, zatim šalje žive logove kontejnera. (Tok logovanja prosleđuje auttentifikacioni kolačić prijavljenog korisnika na hub SignalR `/hubs/logs`, tako da se povezuje umesto pada sa `Invalid negotiation response received`.)

## Skupovi parametara

**Parameter set** je nazvan, ponovno korišćeni skup prepravki parametara cBot-a pohranjen kao ravan JSON objekat mapira svaki naziv parametra na skalarnu vrednost, npr. `{"Period": 14, "Label": "trend"}`. U vreme pokretanja/backtesta pretvara se u cTrader `params.cbotset` datoteku (`{ "Parameters": { … } }`). Možete kreirate/urediti skup kao raw JSON iz dijaloga **Parameter sets** cBot-a ili u letnjem iz dijaloga Run.

Svaki skup parametara **pripada cBot-u**: dijalog New Parameter Set navodi sve vaše cBots i **morate da odaberete jedan** — kreiranje je blokirano dok nije odabran cBot. Ime skupa **je jedinstveno po cBot-u**: kreiranje ili preimenovanje skupa na ime koji je već upotrebljen na istom cBot-u je odbijeno (jasna greška u dijalogu, `409 Conflict` na API-u). Isto ime može biti ponovno upotrebljeno na **drugačijem** cBot-u.

JSON je **validiran** pri čuvanju: mora biti jedan ravan objekat čije su vrednosti sve skalarne (string / broj / bool). Ne-objekatni koren, niz, ugniježđeni objekat, `null` vrednost, ili loše formirani JSON je odbijen (jasna greška u dijalogu, `400 Bad Request` na API-u). Prazan objekat `{}` je dozvoljeno i znači "bez prepravki".

## Napomene CLI-ja cTrader-a

Backtestovi trebaju `--data-mode` (podrazumevano `m1`), datume kao `dd/MM/yyyy HH:mm`, i `params.cbotset` JSON pozicioni arg; `run` odbija `--data-dir` (samo backtest). Pogledaj `ContainerCommandHelpers`.

## Čvorovi i razmera

Kapacitet izvršavanja se skalira dodavanjem čvornih agenata (samoregistracija + otkucaj). Pogledaj [čvor otkrivanja](../operations/node-discovery.md) i [skaliranje](../deployment/scaling.md).

## Trader je obavezan

Pokretanje ili testiranje sa istorijom cBot-a zahteva cTrader trading račun da bi se povezao. Dok ne dodate jedan pod **Trading accounts**, dugmići **Run New cBot** / **Backtest New cBot** su onemogućeni (sa savetu) i stranica prikazuje obaveštenje koje se povezuje sa postavljanjem računa — više ne odlazite u sirov `stream connect failed` greški od bota bez računa.
