---
description: "Napravite, pokrenite, testirajte cBots za cTrader (C# i Python, oba na .NET-u) iz in-browser Monaco IDE-a, pokrenite na zvaničnoj ghcr.io/spotware/ctrader-console slici."
---

# Izrada i testiranje cBots-a

Napravite, pokrenite, testirajte cBot-e za cTrader (C# **i** Python, oba na .NET-u) iz in-browser
Monaco IDE-a, pokrenite na zvaničnoj `ghcr.io/spotware/ctrader-console` slici.

## Izrada

- Stranica **Builder** hostuje Monaco editor; `CBotBuilder` kompajlira projekat sa
  `dotnet build` **u privremenom kontejneru** (`AppOptions.BuildImage`, radni direktorijum bind-mount
  na `/work`), tako da nepouzdan korisnikov MSBuild ne dostigne domaćin. NuGet restore je keširan
  između izrada preko deljenog volumena. Web domaćin mora imati pristup Docker soketu.
- C# + Python startni šabloni nalaze se u `src/Nodes/Builder/Templates/`.

## Pokretanje i testiranje

- **Instance** = TPH hijerarhija stanja (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Prelazak zamenjuje entitet (promena ID-a),
  ID kontejnera se nosi dalje.
- `NodeScheduler` bira najmanje učitan prikladan čvor; `ContainerDispatcherFactory` usmerava na
  udaljeni čvor HTTP agent ili lokalni Docker dispatcher.
- Polisori dovršetka usklađuju izlazne kontejnere (backtest kontejneri se sami gase putem
  `--exit-on-stop`); izveštaj prisutan → dovršeno (čuva `ReportJson`), nedostaje → neuspešno.
- Živi dnevnici kontejnera struju u pretraživač preko SignalR-a; krive kapitala testiranja parsiraju se iz
  izveštaja i prikazuju.

## Podaci tržišta za testiranje keširani po računu

cTrader Console preuzima istorijske tick/bar podatke u svoju `--data-dir`. Taj direktorijum je
**stabilan, trajni keš ključan po trading računu** (njegov broj računa) — bind-mount sa diska čvora
na svojoj putanji kontejnera (`/mnt/data`), **odvojena, ne-ugneždena montaža** od
per-instance radnog direktorijuma. Tako svako testiranje na istom računu **ponovno koristi** već preuzete podatke
umesto da ih ponovno preuzima svaki put. (Ranije je direktorijum podataka bio ispod per-instance radnog direktorijuma, čiji se ID menja svaki run, što je forsiralo svežu
preuzimanje svakog testiranja.) Efemeralni per-instance radni direktorijum i dalje drži algoritam, parametre, lozinku
i izveštaj; deljeni keš podataka se broji u upotrebi podataka testiranja čvora i briše se akcijom čišćenja čvora.

## Postavke testiranja

Dijalog **Backtest** izlaže svaku postavku koju CLI cTrader Console testiranja prihvata, tako da nikad
ne morate dodirivati liniju komande:

- **Od / Do** — prozor testiranja (`--start` / `--end`).
- **Režim podataka** — `m1` (1-minutne bare) ili `tick` (`--data-mode`).
- **Početni saldo** — defaultira na `10000` (`--balance`). A **saldo od 0 ne postavlja trgovine i čini
  cTrader emituje prazan izveštaj koji tada pada** ("Message expected"), tako da se ne-nula saldo
  uvek šalje.
- **Provizija** i **Spread** (`--commission` / `--spread`, spread u pipima).
- **Napredne opcije** — polje slobodnog oblika `name=value` po liniji za bilo koju drugu opciju testiranja koju cTrader
  podržava (npr. `applyCommissionAutomatically=true`); svaka linija postaje `--name value` CLI argument.

## Stranica detalja instance

Otvaranje instance (`/instance/{id}`) prikazuje njen živi status, dnevnike i — za testiranje — krivu kapitala.
**Naslov kartice pretraživača** odražava specifičnu instancu (**ime cBot-a · tip · simbol**, npr.
`TrendBot · Backtest · EURUSD`) tako da se kartica sa živim pokretanjem i kartica testiranja mogu razlikovati na prvi pogled.
Run i testiranje istog cBot-a prate se kao različite **lineaže** (stabilan ID lineaže nošen
preko prelaza stanja), tako da stranica prati tačno jednu instancu i nikad ne meša podatke run-a sa
testiranjem.

## Kontrole životnog ciklusa instance

Svaki red instance (i njegova stranica detalja) ima kontrole ispravne za stanje. Aktivna instanca prikazuje
**Stop**; terminalna (Stopped / Completed / Failed) prikazuje **Start (▶)** da je ponovno pokrene sa
istim cBot-om, računom, simbolom, vremenskom okviru, setom parametara i slikom (run se ponovo pokreće kao run, testiranje kao testiranje). Klik na Stop prikazuje "Stopping…" obaveštenje i onemogućava ikonu dok se ne razriješi, i novo kreirani run se pojavljuje u listi odmah — bez osvežavanja stranice.

Dnevnici konzole su **trajno čuvani kada se instance prekine** — za run (na Stop) i za
**testiranje** (po dovršetku) — tako da dnevnici poslednjeg run-a ostaju vidljivi na stranici detalja i,
preko trake za alate dnevnika, **kopirani u privremenu memoriju** (ikona Copy logs) ili **preuzeti** (ikona Download logs)
čak i nakon što je kontejner nestao. Oba deluju na kompletan konzolni dnevnik instance, ne samo na
vidljiv rep na ekranu.

Učitan `.algo` nikad nije građen ovde, tako da je **Last Build** kolona na cBots stranici prazna
(prikazuje vrijeme izgradnje samo za cBot-e koje gradite u pretraživaču).

## Uređivanje i ponovno pokretanje zaustavljene instance

**Zaustavljena** instance (run ili testiranje) ima kontrolu **Edit** — ikonu na njenoj redama u listi **i**
pored Start/Stop na njenoj stranici detalja — koja otvara dijalog **prethodno popunjen** sa njenom trenutnom konfiguracijom.
Možete promeniti **trading račun, simbol, vremenski okvir, set parametara i oznaku slike** (i, za
testiranje, **prozor i sve postavke testiranja** gore), zatim **Save & start** je ponovno pokreće sa
novim postavkama (zamenom zaustavljene instance). Kontrola je **onemogućena dok je instance aktivna** —
samo zaustavljena instance može biti uređena.

## Pokretanje iz editora koda

Klik na **Run** u editoru koda otvara dijalog umesto da pali slepo, hard-kodirano pokretanje:

- **Trading račun** (obavezno) — cTrader račun kojem se cBot povezuje.
- **Set parametara** (opciono) — izaberite postojeći set, ili ga ostavite prazno za pokretanje sa cBot-ovih
  **podrazumevanih vrednosti parametara**. Dugme **+** pored selektora kreira novi set parametara
  inline (pogledajte ispod) i bira ga.
- **Simbol / Vremenski okvir** defaultiraju na `EURUSD` / `h1` i mogu se promeniti; **Cancel** ili **Run**.

Na **Run** editor čuva + gradi trenutni izvor, pokreće instancu na odabranom računu
sa odabranim parametrima, zatim prati žive dnevnike kontejnera. (Tok dnevnika prosleđuje
autentifikacijski kolačić prijavljenog korisnika do `/hubs/logs` SignalR hub-a, tako da se povezuje umesto da ne uspe sa
`Invalid negotiation response received`.)

## Setovi parametara

**Set parametara** je imenovan, ponovno upotrebljiv set prepisivanja cBot parametara čuvanih kao ravan JSON
objekat mapiranje svakog imena parametra na skalarnu vrednost, npr. `{"Period": 14, "Label": "trend"}`. Na
vremenu pokretanja/testiranja pretvara se u cTrader `params.cbotset` fajl
(`{ "Parameters": { … } }`). Možete kreirati/urediti set kao sirovi JSON iz dijaloga cBot-a **Parameter
sets** ili inline iz dijaloga Run.

Svaki set parametara **pripada cBot-u**: dijalog New Parameter Set navodi sve vaše cBot-e i morate
**izabrati jedan** — kreiranje je blokirano dok nije izabran cBot. Naziv seta je **unikatan po cBot-u**:
kreiranje ili preimenovanje seta u naziv koji drugi set istog cBot-a već koristi je odbijeno (jasna
greška u dijalogu, `409 Conflict` u API-ju). Isto ime može biti ponovno iskorišćeno na **različitom** cBot-u.

JSON je **validiran** na čuvanju: mora biti jedan ravan objekat čije su vrednosti sve skalare
(string / broj / bool). Ne-objekat root, niz, ugneždeni objekat, vrednost `null`, ili malformiran
JSON je odbijen (jasna greška u dijalogu, `400 Bad Request` u API-ju). Prazan objekat `{}`
je dozvoljen i znači "bez prepisivanja".

## Napomene CLI cTrader Console

Testiranja trebaju `--data-mode` (default `m1`), datumi kao `dd/MM/yyyy HH:mm`, i
`params.cbotset` JSON pozicioni arg; `run` odbija `--data-dir` (samo testiranje). Videti
`ContainerCommandHelpers`.

## Čvorovi i skaliranje

Kapacitet izvršavanja se skalira dodavanjem čvor agenata (samoreferenca + otkucaj srca). Videti
[node discovery](../operations/node-discovery.md) i [scaling](../deployment/scaling.md).

## Trading račun je obavezan

Pokretanje ili testiranje cBot-a zahteva cTrader trading račun za povezivanje. Dok ne dodate jedan ispod
**Trading accounts**, dugmadi **Run New cBot** / **Backtest New cBot** su onemogućeni (sa
savetom) i stranica prikazuje poruku koja povezuje na konfiguraciju računa — više ne dobijate sirovu
`stream connect failed` grešku od bot-a bez računa.
