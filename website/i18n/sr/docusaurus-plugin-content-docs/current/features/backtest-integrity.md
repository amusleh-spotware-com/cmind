---
description: "Laboratorija za integritet backtesta — determinističke, korporativne statistike preoptimizovanja (Probabilistički i deflatirani Sharpe, t-statistika) koje pretvore sirovi backtest u verdikt Robustan / Krhak / Preoptimizovan, ispravljajući se za broj konfiguracija koje ste pokušali."
---

# Laboratorija za integritet backtesta

Maloprodajne platforme vam prikazuju Sharpe omjer ili neto dobit backtesta i tu stanu. Institucije nikada ne vjeruju u sirovi backtest — pitaju da li rezultat preživi **ispravku za pristranost selekcije i broj pokušanih konfiguracija**. Laboratorija za integritet backtesta donosi tu provjeru u cMind. To je **deterministička matematika** (bez AI-ja, bez vanjskih poziva), tako da je verdikt reproducibilan i svaki broj je objašnjiv.

Otvorite je na **cBots → Integrity** (`/quant/integrity`).

## Šta se izračunava

Datom seriji povrata (ili krivuljom vlasničkog kapitala/saldo) i broja skupova parametara koje ste pokušali da dosegnete, analizator izvještava:

- **Sharpe omjer** — po periodu i anualizovan (kvadratni korijen vremena).
- **Probabilistički Sharpe omjer (PSR)** — pouzdanost da *pravi* Sharpe nadmaši mjeru, uzimajući u obzir dužinu iskustva, asimetriju i kurtozis (Bailey & López de Prado, 2012). Kraće ili debele-repe iskustvo ga snižava.
- **Deflatiran Sharpe omjer (DSR)** — PSR izmjeren prema **deflatirani mjeri**: Sharpe koji biste očekivali od *najboljih N slučajnih pokušaja* pod nultom hipotezom (Teorem o lažnoj strategiji). Što više konfiguracija pokušate, to je viši standard — ovo je ono što hvata preoptimizovanje.
- **t-statistika** prosječnog povrata. Slijedeći Harveya, Liu-ja i Zhu-ja, pravi edge trebao bi da prođe **t ≥ 3,0**, ne udžbenik 2,0.
- **Asimetrija / kurtozis** povrata, koji hrane PSR/DSR korekcije.

## Verdikt

| Verdikt | Značenje | Pravilo |
|---|---|---|
| **Robustan** | Edge preživa pokušaje koje ste pokrenuli. | DSR ≥ 95% **i** PSR ≥ 95% **i** \|t\| ≥ 3,0 |
| **Krhak** | Statistički živ, ali ne uvjerljivo — ne povećavajte veličinu samo na osnovu ovoga. | između dva |
| **Preoptimizovan** | Najvjerovatnije artefakt pristranosti selekcije, ne pravi edge. | DSR < 90% |

Svaki rezultat nosi obrazloženje na jasnom engleskom jeziku kako "zašto" nikada ne bi trebalo biti skriveno.

## Vjerojatnost preoptimizovanja backtesta (kroz pokušaje)

Hranjenjem broja pokušaja je dobro; hranjenjem **stvarne out-of-sample serije svakog pokušaja koji ste pokušali** je bolje. Zalijepite ih u opcionalno **polje za pokušaje** (jedna serija po liniji) i cMind pokreće **Kombinatorijalno-simetrična unakrsna validacija** (Bailey, Borwein, López de Prado & Zhu, 2015): dijeli opservacije u grupe, i za svaki način odabira polovice kao in-sample bira in-sample najbolju konfiguraciju i provjerava da li taj pobjednika pada u donju polovicu **out-of-sample**. **Vjerojatnost preoptimizovanja backtesta (PBO)** je frakcija razdijela gdje pobjednika nije uspio da se generalizira. PBO blizu 0 znači da je najbolja konfiguracija zaista najbolja; PBO od 0,5 ili više znači da vaš proces selekcije bira buku — verdikt postaje **Preoptimizovan** bez obzira na to kako je dobra izgledala pobjednika.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Kada nativni cTrader Console optimizator dođe, cMind će automatski hraniti njegovog punog pokušaja ovdje.

## Pokušaji — broj koji je bitan

`Trials` je **koliko ste skupova parametara testirali** prije nego što ste odabrali ovaj. Testiranje jedne strategije i testiranje deset hiljada i čuvanje najbolje su divlje različite stvari: druga proizvodi visok in-sample Sharpe slučajno. Hranjenjem činjenidnog broja pokušaja je cijela poenta — to podiže deflaciju i može premjestiti "odličan" backtest u **Preoptimizovan**. Kada nativni cTrader Console optimizator dođe, cMind ga hrani stvarnom veličinom grida pokušaja automatski.

## Ulazi

- **Periodični povrati** — jedan broj po periodu (npr. `0,01` = +1%). Najmanje dva. Polje se validira dok tipkate: broji važeće brojeve, označava bilo koji token koji nije broj, i omogućava **Analize** samo kada su prisutne najmanje dvije čiste vrijednosti (polje za pokušaje omogućava **Procijeni preoptimizovanje** kada su dostupne dvije serije od četiri ili više brojeva svaka).
- **Krivulja vlasničkog kapitala / bilansa** — cMind vam izvođa uzastopne jednostavne povrate.
- **Direktno iz backtesta — bez kopiranja-lijepljenja.** Svaki završeni backtest izlaže štit **Provjeri integritet backtesta** ikona na listu **Backtest** i na prikazu detaljne instance; jedan klik pokreće laboratoriju na skladištenoj krivulji vlasničkog kapitala tog pokretanja i prikazuje verdikt u dijaloškom okviru. Ikona je onemogućena dok se backtest ne završi i ne proizvede izvještaj, tako da to nikada nije mrtva kontrola. Ispod haube ovo je `POST /api/quant/integrity/backtest/{instanceId}`, koji čita krivulju vlasničkog kapitala pohrantenog izvještaja.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Vraća verdikt, sve metrike i obrazloženje. `POST /api/quant/integrity/backtest/{id}` pokreće istu analizu na završenom backtestmu koji posjedujete.

## Zašto je pouzdano

Statistika su čiste funkcije u domeni jezgre (`Core.Quant`) sa nultim zavisnostima od infrastrukture — ne mogu biti izbačene mrežnom kvarom, i učvršćene su vektorima jedinstvenih testova sa zlatnim vektorima protiv objavljenih formula. Normalni CDF/inverz su aproksimacije zatvorene forme (Abramowitz-Stegun / Acklam), tako da isti ulazi uvijek daju isti verdikt.
