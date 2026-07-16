---
description: "Pravite, pokrenite, backtestirajte cTrader cBots (C# i Python, oba .NET) iz ugrađenog Monaco editora u pregledniku, pokrenite na zvaničnoj ghcr.io/spotware/ctrader-console slici."
---

# Build & backtest cBots

Pravite, pokrenite, backtestirajte cTrader cBots (C# **i** Python, oba .NET) iz ugrađenog Monaco
editora u pregledniku, pokrenite na zvaničnoj `ghcr.io/spotware/ctrader-console` slici.

## Build

- Stranica **Builder** hostira Monaco editor; `CBotBuilder` kompajlira projekat sa
  `dotnet build` **u jednokratnom kontejneru** (`AppOptions.BuildImage`, radni direktorijum bind-mount
  na `/work`), tako da nepouzdan korisnikov MSBuild ne može dosegnuti domaćina. NuGet restore je keširan
  između izgradnji preko deljene zapremine. Web domaćin treba pristup Docker soketu.
- C# + Python starter šabloni se nalaze u `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH hijerarhija stanja (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Prelazak zameni entitet (promena id),
  id kontejnera se nosi dalje.
- `NodeScheduler` bira najmanje opterećeni prikladan čvor; `ContainerDispatcherFactory` usmeri ka
  udaljenom čvoru HTTP agenta ili lokalnom Docker dispečeru.
- Completion pollers usklađuju izašle kontejnere (backtest kontejneri se sami izlaze preko
  `--exit-on-stop`); izveštaj prisutan → završen (čuva `ReportJson`), nedostaje → neuspeo.
- Živi logs kontejnera struju u preglednik preko SignalR; equity krive backtesta se raščlanjuju iz
  izveštaja i grafikuju.

## Backtest market data is cached per account

cTrader Console preuzima istorijske tick/bar podatke u svoju `--data-dir`. Taj direktorijum je
**stabilna, trajna keš ključana na trading računu** (njegov broj računa) — bind-mount sa diska čvora
na njegovu sopstvenu putanju kontejnera (`/mnt/data`), **odvojena, neugnježdena montaža** od
po-instance radnog dir. Dakle, svaki backtest na istom računu **poново koristi** već preuzete podatke
umesto ponovnog preuzimanja svakog pokretanja. (Ranije je
data dir živio pod po-instance radnom dir, čiji se id menja svaki put, što je forsiralo svež
preuzimanje svakog backtesta.) Efemerni po-instance radni dir i dalje drži algo, parametre, lozinku
i izveštaj; deljeni data keš se broji u backtest-data upotrebi čvora i briše sa čvora-čist akcijom.

## Backtest settings

**Backtest** dijalog izlazi svaku postavku koju cTrader Console backtest CLI prihvata, tako da nikada
ne morate dodirivati komandnu liniju:

- **From / To** — backtest prozor (`--start` / `--end`).
- **Data mode** — jedan od tri cTrader moda (`--data-mode`): **Tick data** (`tick`, tačno),
  **m1 bars** (`m1`, brzo), ili **Open prices only** (`open`, najbrže).
- **Starting balance** — zadana vrednost je `10000` (`--balance`). A **0 bilans ne postavlja trgovine i čini
  cTrader emituje prazan izveštaj koji onda pada** ("Message expected"), tako da se uvek šalje
  nenula bilans.
- **Commission** i **Spread** — `--commission` / `--spread` (spread u pipima).
- **Data file** (opciono) — putanja na čvoru ka istorijskoj datoteci podataka (`--data-file`); ostavite
  prazno za korišćenje preuzete/keširane podataka.
- **Expose environment variables** — prebacivanje koje prosleđuje varijable okruženja domaćina cBotu
  (zastavica `--environment-variables`).

## Instance detail page

Otvaranje instance (`/instance/{id}`) pokazuje njen živ status, logs i — za backtest — equity
krivu. **Naslov kartice preglednika** odražava specifičnu instancu (**cBot ime · vrsta · simbol**, npr.
`TrendBot · Backtest · EURUSD`) tako da se run kartica i backtest kartica mogu razlikovati na prvi
pogled. Run i backtest istog cBota se prate kao različitih **lineaža** (stabilan id lineaže nošen
kroz prelaze stanja), tako da stranica prati tačno jednu instancu i nikad ne meša podatke runa sa
backtestom.

## Instance lifecycle controls

Svaki red instance (i njena detaljna stranica) ima stanja-ispravne kontrole. **Aktivna** instanca
pokazuje **Stop**; **terminalna** (Stopped / Completed / Failed) pokazuje **Start (▶)** da je ponovo
pokrene sa istim cBotom, računom, simbolom, vremenskim okvirom, skupom parametara i slikom (run se
restartuje kao run, backtest kao backtest). Klikom na Stop prikazuje se obaveštenje "Stopping…" i
onemogućava ikonu dok se ne razreši, a novokreirani run se pojavljuje na listi odmah — bez osvežavanja
stranice.

Console logs se **čuvaju kada se instanca završi** — za run (na Stop) i za
**backtest** (na završetku) — tako da se logovi poslednjeg pokretanja čuvaju vidljivi na detaljnoj
stranici i, preko toolbar-a logovanja, **kopirani u privremenu memoriju** (Kopuj logs ikona) ili
**preuzeti** (Preuzmi logs ikona) čak i nakon što je kontejner otišao. Oba deluju na puni console log
instance, ne samo vidljivi rep.

Učitan `.algo` nikad nije izgrađen ovde, tako da njegova **Last Build** kolona na cBots stranici
ostaje prazna (pokazuje vreme izgradnje samo za cBots koje pravite u pregledniku).

## Edit & re-run a stopped instance

**Zaustavljena** instanca (run ili backtest) ima **Edit** kontrolu — ikona na njenoj rednici na listi
**i** pored Start/Stop na njenoj detaljnoj stranici — koja otvara dijalog **prethodno popunjen** sa
nenom trenutnom konfiguracijom. Možete promeniti **trading račun, simbol, vremenski okvir, skup
parametara i tag slike** (i, za backtest, **prozor i sve backtest postavke** iznad), zatim
**Save & start** je ponovo pokreće sa novim postavkama (zamenjuje zaustavljena instanca). Kontrola je
**onemogućena dok je instanca aktivna** — samo zaustavljena instanca može biti uređena.

## Run from the code editor

Klikom na **Run** u editoru koda otvara se dijalog umesto da se opalio slepog, hardkodiranog runa:

- **Trading account** (obavezno) — cTrader račun kojem se cBot povezuje.
- **Parameter set** (opciono) — odaberite postojeći skup, ili ostavite prazno da pokrenete sa cBot
  **podrazumevanim vrednostima parametara**. **+** dugme pored selektora pravi novi skup parametara
  inline (pogledajte ispod) i bira ga.
- **Symbol / Timeframe** zadana vrednost je `EURUSD` / `h1` i mogu biti promenjena; **Cancel** ili **Run**.

Na **Run** editor čuva + gradi trenutni izvor, pokreće instancu na odabranom računu
sa odabranim parametrima, zatim prati žive logove kontejnera. (Tok logovanja prosleđuje kolačić za
autentifikaciju ulogovanog korisnika na `/hubs/logs` SignalR hub, tako da se povezuje umesto pada sa
`Invalid negotiation response received`.)

## Parameter sets

**Parameter set** je imenovan, ponovno upotrebljiv skup cBot prepisivanja parametara pohranjen kao
ravan JSON objekat mapiranje svakog imena parametra na skalarnu vrednost, npr. `{"Period": 14, "Label": "trend"}`. Na
vremenu pokretanja/backtesta se pretvara u cTrader `params.cbotset` datoteku
(`{ "Parameters": { … } }`). Možete praviti/uređivati skup kao sirov JSON iz cBot **Parameter
sets** dijaloga ili inline iz Run dijaloga.

Svaki skup parametara **pripada cBotu**: dijalog New Parameter Set navodi sve vaše cBots i vi
**morate izabrati jedan** — kreiranje je blokirano dok cBot nije izabran. Ime seta **je jedinstveno
po cBotu**: pravljenje ili preimenovanje seta u ime koje drugi skup istog cBota već koristi je
odbijeno (jasan greška u dijalogu, `409 Conflict` na API-ju). Isto ime može biti ponovno upotrebljeno
na **drugom** cBotu.

JSON je **validiran** pri čuvanju: mora biti jedan ravan objekat čije vrednosti su sve skalarne
(string / number / bool). Neobjektni koren, niz, ugneždeni objekat, `null` vrednost, ili
loše oblikovan JSON je odbijen (jasan greška u dijalogu, `400 Bad Request` na API-ju). Prazan
objekat `{}` je dozvoljeno i znači "nema prepisivanja".

## cTrader Console CLI notes

Backtesti trebaju `--data-mode` (zadana vrednost `m1`), datumi kao `dd/MM/yyyy HH:mm`, i
`params.cbotset` JSON pozicioni arg; `run` odbija `--data-dir` (samo backtest). Pogledajte
`ContainerCommandHelpers`.

## Nodes & scale

Kapacitet izvršavanja razmere dodavanjem čvora agenata (samo-registracija + heartbeat). Pogledajte
[node discovery](../operations/node-discovery.md) i [scaling](../deployment/scaling.md).

## A trading account is required

Pokretanje ili backtestiranje cBota trebaju cTrader trading račun za konekciju. Dok ga ne dodate ispod
**Trading accounts**, dugmadi **Run New cBot** / **Backtest New cBot** su onemogućeni (sa
alatnom podesavanjem) i stranica pokazuje poruku koja se povezuje sa postavljanjem računa — vi
više ne pogađate sirov `stream connect failed` greška iz bota bez računa.
