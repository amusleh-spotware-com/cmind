---
description: "Izgradite, pokrenite i testirajte cTrader cBots (C# i Python, oba .NET) iz pretraživača Monaco IDE-a, pokrenuto na zvaničnoj ghcr.io/spotware/ctrader-console slici."
---

# Izgradnja i testiranje cBots-a

Izgradite, pokrenite i testirajte cTrader cBots (C# **i** Python, oba .NET) iz pretraživača Monaco
IDE-a, pokrenuto na zvaničnoj `ghcr.io/spotware/ctrader-console` slici.

## Izgradnja

- **Builder** stranica hostira Monaco editor; `CBotBuilder` kompajlira projekat sa
  `dotnet build` **u privremenom kontejneru** (`AppOptions.BuildImage`, radni direktorijum bind-mount
  na `/work`), tako da nepouzdani korisnici MSBuild ne dostignu domaćina. NuGet restauracija se keširuje
  između gradnji preko deljene zapremine. Web domaćin treba pristup Docker socketu.
- C# + Python starter šabloni se nalaze u `src/Nodes/Builder/Templates/`.

## Pokretanje i testiranje

- **Instances** = TPH stanja hijerarhija (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Prelazak zamenjuje entitet (promena id-a),
  container id se prenosi.
- `NodeScheduler` bira najmanje opterećeni kvalifikovani čvor; `ContainerDispatcherFactory` usmeri na
  udaljeni čvor HTTP agent ili lokalni Docker dispatcher.
- Pollers za dovršetak usklađuju izlazne kontejnere (backtest kontejneri se sami izlaze preko
  `--exit-on-stop`); izveštaj prisutan → završen (skladišti `ReportJson`), nedostaje → neuspešan.
- Direktni logovi kontejnera se strujaju u pretraživač preko SignalR; backtest equity krive se analiziraju iz
  izveštaja + crtanje.

## Backtest tržišnih podataka je keširano po računu

cTrader Console preuzima istorijske tick/bar podatke u svoj `--data-dir`. Taj direktorijum je
**stabilan, trajni keš ključan na trgovački račun** (njegov broj računa) — bind-mount sa
diska čvora na njegovoj sopstvenoj putanji kontejnera (`/mnt/data`), **odvojena, ne-ugneždena mount** od
po-instance radnog direktorijuma. Dakle, svaki backtest na istom računu **ponovno koristi** već preuzete podatke
umesto da ih ponovo preuzme. (Ranije je direktorijum podataka živeo ispod po-instance radnog direktorijuma, čiji id se
menja sa svakim pokretanjem, što je forsiralo novo preuzimanje sa svakim backtestom.) Efemeralni po-instance radni direktorijum
i dalje sadrži algo, parametre, lozinku i izveštaj; deljena keš podataka se računa u korišćenju backtest-podataka čvora i briše se
akcijom čvor-čistanje.

## Postavke backtesta

**Backtest** dijalog izlažu korisniku podešljive cTrader Console backtest postavke, tako da nikada ne morate
dodiriti komandnu liniju:

- **Symbol / Timeframe** — vremenski okvir je **padajuća lista svakog cTrader perioda** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` i Renko/Range/Heikin periodi), u
  konzoli kanonskom obliku, tako da uvek birate validnu `--period`.
- **From / To** — backtest prozor (`--start` / `--end`).
- **Data mode** — jedan od tri cTrader moda (`--data-mode`): **Tick data** (`tick`, tačna),
  **m1 bars** (`m1`, brza), ili **Open prices only** (`open`, najbrža).
- **Starting balance** — podrazumevano `10000` (`--balance`). **Balans od 0 ne postavlja nikakve trgovine i čini
  da cTrader emituje prazan izveštaj koji zatim pada** ("Message expected"), tako da se uvek šalje ne-nula balans.
- **Commission** — `--commission`.
- **Spread** — `--spread`, **numeričko polje u pipama koje ne može biti ispod 0**. Ono je **skriveno u Tick
  data modu**, gde cTrader derivira spread iz samih tick podataka (nije poslana `--spread`).

Direktorijum podataka (`--data-file` / `--data-dir`) upravlja sama aplikacija (po-račun keš, videti
gore), nije izložen u dijalogu.

:::note cTrader pada na praznom testu
Ako backtest ne proizvede **nikakve rezultate** — nikakve trgovine ili nikakvi tržišni podaci za izabrane datume/simbol —
cTrader Console-ov pisac izveštaja baca `Message expected` i izlazi bez izveštaja. Aplikacija ne može
popraviti taj upstream bug, ali ga detektuje i označava instancu **Failed** sa jasnom razlogom
("no backtest results for the selected range…") umesto neobrađene stack tragove. Odaberite širi raspon datuma
koji ima dostupne tržišne podatke i pokušajte ponovo.
:::

## Stranica detaljnog pregleda instance

Otvaranje instance (`/instance/{id}`) prikazuje njen direktan status, logove i — za backtest — equity
krivu. **Naslov kartice pretraživača** odražava specifičnu instancu (**cBot naziv · vrsta · simbol**, npr.
`TrendBot · Backtest · EURUSD`) tako da su direktna pokretanja i backtest kartice razlikovane na prvi pogled.
Run i backtest istog cBot-a se prate kao zasebne **linije** (stabilan lineage id prenet
kroz prelazake stanja), tako da stranica prati tačno jednu instancu i nikad ne meša podatke runa sa backtestom.

## Kontrole životnog ciklusa instance

Svaki red instance (i njegova stranica detaljnog pregleda) ima kontrole prikladne stanju. **Aktivna** instanca prikazuje
**Stop**; **terminalna** (Stopped / Completed / Failed) prikazuje **Start (▶)** da je ponovno pokrene sa
istim cBot-om, računom, simbolom, vremenskim okvirom, setom parametara i slikom (run se ponovno pokreće kao run, backtest kao backtest). Klik na Stop prikazuje obaveštenje "Stopping…" i onemogućava ikonu dok se ne razriješi, a novo kreirani run se pojavljuje u listi odmah — bez osvežavanja stranice.

Console logovi su **trajno sačuvani kada se instanca završi** — za run (na Stop) i za
**backtest** (na dovršetku) — tako da se logovi poslednjeg runa ostaju vidljivi na stranici detaljnog pregleda i,
preko log alatne trake, **kopirati u privremenu memoriju** (Copy logs ikona) ili **preuzeti** (Download logs
ikona) čak i posle nego što je kontejner nestao. Oba deluju na puni console log instance, ne samo na
vidljiva rep.

**Završen backtest** takođe zadržava svoj **cTrader izveštaj** u oba formata — surovi **JSON**
(istu koju koriste equity kriva i AI analiza) i puni **HTML** izveštaj. Oba se mogu preuzeti iz reda backtesta **i** stranice detaljnog pregleda preko dedicirane ikone. Samo **poslednji runovi**
izveštaji se čuvaju, i ikone su **onemogućene** za svaki backtest koji nije počet, pokrenuta ili
neuspešan (i nikada se ne prikazuju za run instancu) — samo završen backtest ima izveštaj za preuzimanje.

**Uploadovana** `.algo` nikada nije izgrađena ovde, tako da njena **Last Build** kolona na cBots stranici
ostaje prazna (prikazuje vreme izgradnje samo za cBots koje gradite u pretraživaču).

## Uredi i ponovno pokreni zaustavljenu instancu

**Zaustavljena** instanca (run ili backtest) ima **Edit** kontrolu — ikonu na njenoj red-u u listi **i**
pored Start/Stop na njenoj stranici detaljnog pregleda — koja otvara dijalog **prethodno popunjen** sa
njenoj trenutnoj konfiguraciji. Možete promeniti **trading account, symbol, timeframe, parameter set i image tag** (i, za
backtest, **prozor i sve gore navedene backtest postavke**), zatim **Save & start** je ponovno pokreće sa
novim postavkama (zamenjujući zaustavljenu instancu). Kontrola je **onemogućena dok je instanca aktivna** —
samo zaustavljena instanca se može uređivati.

## Pokreni iz editora koda

Klikom na **Run** u editoru koda otvara se dijalog umesto slepo poklapanja:

- **Trading account** (obavezno) — cTrader račun kojem se cBot povezuje.
- **Parameter set** (opciono) — odaberite postojeći set, ili ga ostavite prazno da pokrenete sa cBot-om
  **podrazumevanim vrednostima parametara**. **+** dugme pored selektora kreira novi set parametara
  inline (videti ispod) i ga bira.
- **Symbol / Timeframe** podrazumevano `EURUSD` / `h1` i mogu se promeniti; **Cancel** ili **Run**.

Na **Run** editor čuva + gradi trenutni izvor, pokreće instancu na odabranom računu
sa odabranim parametrima, zatim prati direktne logove kontejnera. (Tok logova napređuje
potpisanog korisnika auth kolačić do `/hubs/logs` SignalR hub, tako da se povezuje umesto da padne sa
`Invalid negotiation response received`.)

## Skupovi parametara

**Parameter set** je namenski, ponovljiv skup cBot parametra prevladavanja sačuvan kao ravan JSON
objekat mapiranje svakog imena parametra na skalarnu vrednost, npr. `{"Period": 14, "Label": "trend"}`. Na
run/backtest vremenu se pretvara u cTrader `params.cbotset` fajl
(`{ "Parameters": { … } }`). Možete praviti/uređivati set kao surovu JSON iz cBot-a **Parameter
sets** dijalog ili inline iz Run dijaloga.

Svaki set parametara **pripada cBot-u**: New Parameter Set dijalog navodi sve vaše cBot-s i vi
**morate izabrati jedan** — kreiranje je blokirano dok se cBot ne odabere. Set **ime je jedinstveno po cBot-u**:
kreiranje ili preimenovanje seta na ime koji drugi set istog cBot-a već koristi je odbijen (jasna
greška u dijalogu, `409 Conflict` na API-ju). Isto ime može biti korišćeno na **drugom** cBot-u.

JSON je **validiran** na čuvanju: mora biti jedan ravan objekat čije vrednosti su sve skalarne
(string / broj / bool). Ne-objekat root, niz, ugneždeni objekat, `null` vrednost ili
malformiran JSON je odbijen (jasna greška u dijalogu, `400 Bad Request` na API-ju). Prazan objekat `{}`
je dozvoljen i znači "nema prevladavanja".

## Napomene cTrader Console CLI-ja

Backtesti trebaju `--data-mode` (podrazumevano `m1`), datume kao `dd/MM/yyyy HH:mm`, i
`params.cbotset` JSON pozicioni arg; `run` odbija `--data-dir` (samo backtest). Videti
`ContainerCommandHelpers`.

## Čvorovi & skaliranje

Kapacitet izvršavanja se širi dodavanjem čvornih agenata (samo-registruj + srčani udarac). Videti
[node discovery](../operations/node-discovery.md) i [scaling](../deployment/scaling.md).

## Trading account je obavezan

Pokretanje ili testiranje cBot-a zahteva cTrader trading account za povezivanje. Dok ne dodate jedan pod
**Trading accounts**, **Run New cBot** / **Backtest New cBot** dugmadi su onemogućeni (sa
savetnikom) i stranica prikazuje poziv koji je povezan do postave računa — više ne dobijate surovu
`stream connect failed` grešku od bota bez računa.
