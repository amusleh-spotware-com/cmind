---
description: "Gradite, pokrenite, testirajte cTrader cBots (C# i Python, oba .NET) iz pregledačkog Monaco IDE-a, pokrenite na zvaničnoj ghcr.io/spotware/ctrader-console slici."
---

# Gradnja i testiranje cBots-a

Gradite, pokrenite, testirajte cTrader cBots (C# **i** Python, oba .NET) iz pregledačkog Monaco
IDE-a, pokrenite na zvaničnoj `ghcr.io/spotware/ctrader-console` slici.

## Gradnja

- **Builder** stranica hostuje Monaco editor; `CBotBuilder` kompajlira projekat sa
  `dotnet build` **u jednokratnom kontejneru** (`AppOptions.BuildImage`, radni direktorijum bind-mount
  na `/work`), tako da nepouzdani korisnik MSBuild ne može dosegnuti domaćina. NuGet restauracija keširana
  između gradnji preko deljene zapremine. Web domaćin mora da ima pristup Docker socketu.
- C# + Python starter šablone žive u `src/Nodes/Builder/Templates/`.

## Pokretanje i testiranje

- **Instanse** = TPH hierarhija stanja (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Prelazak zamenjuje entitet (promena ID-a),
  ID kontejnera se prenosi.
- `NodeScheduler` bira najmanje opterećeni kvalifikovani čvor; `ContainerDispatcherFactory` rute
  do udaljenog čvora HTTP agenta ili lokalnog Docker dispečera.
- Pollers-i završetka usklađuju izlazne kontejnere (backtest kontejneri se sami izlaze preko
  `--exit-on-stop`); izveštaj prisutan → završen (skladišti `ReportJson`), odsutan → neuspešan.
- Direktni logovi kontejnera se prenose pregledniku preko SignalR; backtest krive kapitala analizirane iz
  izveštaja + grafikovane.

## Backtest tržišni podaci se keširaju po računu

cTrader Console preuzima istorijske tick/bar podatke u svoj `--data-dir`. Taj direktorijum je
**stabilna, trajna keš ključana na trgovački račun** (njegovo broju računa) — bind-mountana sa
diska čvora na njegovoj sopstvenoj putanji kontejnera (`/mnt/data`), **odvojena, neugnježdena mount** od
direktorijuma za rad po instanci. Zato se svaki backtest na istom računu **ponovno koristi** već preuzeti podaci
umesto ponovnog preuzimanja pri svakom pokretanju. (Ranije je direktorijum podataka živeo ispod direktorijuma rada po instanci, čiji se ID menja sa svakim pokretanjem, što je forsiralo novo
preuzimanje sa svakim backtestom.) Efemeralni direktorijum rada po instanci i dalje sadrži algoritam, parametre, lozinku
i izveštaj; deljena keš podataka se računa u korišćenju backtest-podataka čvora i briše pomoću
akcije čvor-čist.

## Postavke backtesta

Dijalog **Backtest** izlažu korisniku podesive cTrader Console backtest postavke, tako da nikad ne morate
dodirivati komandnu liniju:

- **Simbol / Vremenski okvir** — vremenski okvir je **padajuća lista svakog cTrader perioda** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` i Renko/Range/Heikin periodi), u
  konzoli kanonskom pravu, tako da uvek birate validan `--period`.
- **Od / Do** — backtest prozor (`--start` / `--end`).
- **Režim podataka** — jedan od tri cTrader režima (`--data-mode`): **Tick podaci** (`tick`, tačan),
  **m1 šipke** (`m1`, brz), ili **Samo otvarajuće cene** (`open`, najbrži).
- **Početni saldo** — zadano `10000` (`--balance`). **Saldo od 0 ne postavlja nikakve trgovine i čini
  da cTrader emituje prazan izveštaj koji tada pada** ("Poruka očekivana"), tako da se uvek šalje saldo različit od nule.
- **Provisija** — `--commission`.
- **Spread** — `--spread`, **numeričko polje u pipima koje ne može biti ispod 0**. Ono je **skriveno u Tick
  režimu podataka**, gde cTrader izvlači spread iz samih tick podataka (nije poslata `--spread`).

Direktorijum podataka (`--data-file` / `--data-dir`) upravlja ga sama aplikacija (po-račun keš, videti
gore), nije izložen u dijalogu.

:::note cTrader pada na praznoj backtestu
Ako backtest ne proizvede **nikakve rezultate** — nikakve trgovine ili nikakvi tržišni podaci za izabrane datume/simbol —
cTrader Console-a sops pisca izveštaja baca `Poruka očekivana` i izlazi bez izveštaja. Aplikacija ne može
popraviti taj upstream bug, ali ga detektuje i označava instancu **Neuspešnom** sa jasom razlogom
("nema backtest rezultata za izabrani raspon…") umesto neobrađene greške u stack trace-u. Izaberite širi opseg datuma
koji sadrži dostupne tržišne podatke i pokušajte ponovo.
:::

## Stranica detalja instanse

Otvaranje instance (`/instance/{id}`) prikazuje njen direktan status, logove i — za backtest — krivu kapitala.
**Naslov kartice pregledača** odražava specifičnu instancu (**naziv cBot-a · vrsta · simbol**, npr.
`TrendBot · Backtest · EURUSD`) tako da se kartica direktnog pokretanja i kartica backtesta lako razlikuju na prvi pogled.
Pokretanje i backtest ista cBota se prate kao zasebne **linije** (stabilna ID linija preneta
preko prelaza stanja), tako da stranica prati tačno jednu instancu i nikad ne mesra podatke pokretanja sa backtestom.

## Kontrole životnog ciklusa instance

Svaki red instance (i njegova stranica detalja) ima kontrole kako je prikladna stanju. Aktivna **instansa** prikazuje
**Stop**; terminalna (Zaustavljeno / Završeno / Neuspešno) prikazuje **Pokretanje (▶)** da je ponovo pokrene sa
istim cBotom, računom, simbolom, vremenskim okvirom, setom parametara i slikom (pokretanje se pokreće kao pokretanje, backtest kao backtest). Klik na Stop prikazuje obaveštenje "Zaustavljanje…" i onemogućava ikonu dok se ne razriješi, a novo kreirano pokretanje se pojavljuje u listi odmah — bez osvežavanja stranice.

Logovi konzole su **trajno sačuvani kada se instanca okončava** — za pokretanje (na Stop) i za
**backtest** (na završetku) — tako da se logovi poslednjeg pokretanja ostaju vidljivi na stranici detalja i,
preko alatne trake logova, **kopiran u privremenu memoriju** (Kopiranje ikone logova) ili **preuzet** (Preuzimanje logova
ikone) čak i nakon što je kontejner otišao. Oba deluje na puni konzolu log instance, ne samo na
vidljiv rep.

Učitana `.algo` nikad nije izgrađena ovde, tako da je njena **Zadnja gradnja** kolona na stranici cBots-a ostavljena
prazna (prikazuje vreme gradnje samo za cBots koje gradite u pregledniku).

## Uređivanje i ponovno pokretanje zaustavljene instance

Zaustavljena **instansa** (pokretanje ili backtest) ima kontrolu **Uređivanja** — ikonu na svom redu u listi **i**
pored Pokretanja/Zaustavljanja na stranici detalja — što otvara dijalog **prethodno popunjen** sa njenom trenutnom konfiguracijom.
Možete promeniti **trgovački račun, simbol, vremenski okvir, set parametara i tag slike** (i, za
backtest, **prozor i sve backtest postavke** gore), zatim **Sačuvaj i započni** ga ponovno pokreće sa
novim postavkama (zamenom zaustavljene instance). Kontrola je **onemogućena dok je instanca aktivna** —
samo zaustavljena instanca može biti uređena.

## Pokretanje iz editora koda

Klikom na **Pokretanje** u editoru koda otvara se dijalog umesto neslomljivo pokretanja:

- **Trgovački račun** (obavezno) — cTrader račun kojem se cBot povezuje.
- **Set parametara** (opciono) — izaberite postojeći set ili ga ostavite prazno da pokrenete sa cBotom
  **zadanim vrednostima parametara**. Dugme **+** pored selektora kreira novi set parametara
  u samoj liniji (videti ispod) i ga bira.
- **Simbol / Vremenski okvir** zadano `EURUSD` / `h1` i mogu se promeniti; **Otkaži** ili **Pokretanje**.

Na **Pokretanje** editor čuva + gradi trenutni izvor, startuje instancu na izabranom računu
sa izabranim parametrima, zatim prati direktne logove kontejnera. (Tok logova šalje kolačić
prijavljenog korisnika u `/hubs/logs` SignalR hub, tako da se povezuje umesto da ne uspe sa
`Nevalidan odgovor pregovora primljen`.)

## Skupovi parametara

**Set parametara** je naveden, ponovno korišćen set cBot preloga parametara sačuvan kao ravan JSON
objekat mapiranje svakog imena parametra na skalarnu vrednost, npr. `{"Period": 14, "Label": "trend"}`. Pri
pokretanju/backtest vremenu se pretvara u cTrader `params.cbotset` fajl
(`{ "Parameters": { … } }`). Možete kreirate/urediti set kao sirovi JSON iz cBotovog dijaloga **Skupova parametara**
ili u samoj liniji iz dijaloga Pokretanja.

Svaki set parametara **pripada cBotu**: dijalog Novi set parametara navodi sve vaše cBots i vi
**morате izabrati jedan** — kreiranja je blokirano dok se cBot ne izabere. Ime seta **je jedinstveno po cBotu**:
pravljenje ili preimenovanje seta na ime koje drugi set istog cBot-a već koristi je odbijeno (jasna
greška u dijalogu, `409 Konflikt` na API-ju). Isto ime se može ponovo koristiti na **drugom** cBotu.

JSON je **validiran** na čuvanju: mora biti jedan ravan objekat čije vrednosti su sve skalarne
(string / broj / bool). Neobjektivna glavna vrijednost, niz, ugnježdeni objekat, `null` vrijednost ili masovljivi
JSON je odbijen (jasna greška u dijalogu, `400 Loš zahtev` na API-ju). Prazan objekat `{}`
je dozvoljen i znači "bez preloga".

## Napomene CLI-ja cTrader Console

Backtesti trebaju `--data-mode` (zadano `m1`), datume kao `dd/MM/yyyy HH:mm`, i
`params.cbotset` JSON pozicioni arg; `run` odbija `--data-dir` (samo backtest). Videti
`ContainerCommandHelpers`.

## Čvorovi i skala

Kapacitet izvršavanja se skalira dodavanjem čvora agenata (samoreg + srčane frekvencije). Videti
[otkrivanje čvorova](../operations/node-discovery.md) i [skaliranje](../deployment/scaling.md).

## Obavezan je trgovački račun

Pokretanje ili testiranje cBot-a zahteva cTrader trgovački račun za povezivanje. Dok ne dodate jedan pod
**Trgovački računi**, dugmići **Pokretanje novog cBot-a** / **Testiranje novog cBot-a** su onemogućeni (sa
alatnom linijom) i stranica prikazuje brzo pozivnu konekciju do postave računa — više ne dobijate neobrađenu
`stream connect failed` grešku od bota bez računa.
