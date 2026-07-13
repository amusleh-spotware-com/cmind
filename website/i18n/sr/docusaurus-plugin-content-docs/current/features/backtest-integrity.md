---
description: "Backtest Integrity Lab — determinističke, fund-grade statistike preprilagođavanja (Probabilistic & Deflated Sharpe, t-stat) koje pretvaraju sirovi backtest u Robust / Fragile / Overfit presudu, ispravljajući za broj konfiguracija koje ste probali."
---

# Backtest Integrity Lab

Retail platforme vam pokazuju backtest Sharpe ili neto profit i tu stanu. Institucije nikad ne veruju sirovom
backtest-u — pitaju da li rezultat preživljava **korekciju za selection bias i broj
konfiguracija probanih**. Backtest Integrity Lab donosi tu proveru u cMind. To je **deterministička
matematika** (nema AI, nema eksternih poziva), tako da je presuda reproduktivna i svaki broj je objašnjiv.

Otvorite ga na **cBots → Integrity** (`/quant/integrity`).

## Šta računa

S obzirom na return series (ili equity/balance curve) i broj parameter sets koje ste probali da dođete
do njega, analyzer raportuje:

- **Sharpe ratio** — per-period i annualized (square-root-of-time).
- **Probabilistic Sharpe Ratio (PSR)** — poverenje da *true* Sharpe beats benchmark,
  uzimajući u obzir dužinu track record-a, skewness i kurtosis (Bailey & López de Prado, 2012). Kratak ili
  fat-tailed record ga smanjuje.
- **Deflated Sharpe Ratio (DSR)** — PSR meren naspram **deflated benchmark-a**: Sharpe koji biste očekivali od *najboljeg od N random trials* pod null (False Strategy Theorem). Što više
  konfiguracija ste probali, to je veća prečka — ovo hvata overfitting.
- **t-statistic** srednjeg return-a. Following Harvey, Liu & Zhu, genuin edge treba da clearing **t ≥ 3.0**,
  ne textbook 2.0.
- **Skewness / kurtosis** returns-a, koji utiču na PSR/DSR korekcije.

## Presuda

| Presuda | Značenje | Pravilo |
|---|---|---|
| **Robust** | Edge preživljava probe koje ste pokrenuli. | DSR ≥ 95% **i** PSR ≥ 95% **i** |t| ≥ 3.0 |
| **Fragile** | Statistički živ ali ne ubedljivo — ne povećavajte veličinu na osnovu samo ovoga. | između dva |
| **Overfit** | Verovatno artefakt selection bias-a, ne genuin edge. | DSR < 90% |

Svaki rezultat nosi plain-English obrazloženje tako da je „zašto" nikad skriveno.

## Probability of Backtest Overfitting (kroz trials)

Unošenje *count* trial-a je dobro; unošenje *stvarnog out-of-sample series-a svake konfiguracije koju ste
probali* je bolje. Nalepite ih u opcioni **trial grid** (jedan series po liniji) i cMind pokreće
**Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu, 2015): deli
opservacije u grupe, i za svaki način izbora polovine kao in-sample bira najbolju in-sample
konfiguraciju i proverava da li taj pobednik pada u donju polovinu **out-of-sample**. **Probability of
Backtest Overfitting (PBO)** je frakcija split-ova gde pobednik nije uspeo da generalizuje. PBO blizu 0
znači da je najbolja konfiguracija genuino najbolja; PBO od 0.5 ili više znači da vaš proces
selekcije bira šum — presuda postaje **Overfit** bez obzira koliko je pobednik dobro izgledao.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Kada native cTrader Console optimizer bude dostupan, cMind će ovde automatski ubaciti njegovu punu trial površinu.

## Trials — broj koji je bitan

`Trials` je **koliko parameter sets-a ste testirali** pre nego što ste izabrali ovaj. Testiranje jedne strategije i
testiranje deset hiljada i zadržavanje najbolje su dramatično različite stvari: drugo proizvodi
visok in-sample Sharpe slučajno. Unošenje iskrenog trial count-a je poenta — podiže
deflation i može premestiti „odličan" backtest u **Overfit**. Kada native cTrader Console optimizer
bude dostupan, cMind će automatski ubaciti pravu veličinu sweep-a.

## Ulazi

- **Periodic returns** — jedan broj po periodu (npr. `0.01` = +1%). Najmanje dva.
- **Equity / balance curve** — cMind izvodi uzastopne simple returns za vas.
- Ili pokrenite direktno na završenom backtest-u: `POST /api/quant/integrity/backtest/{instanceId}` čita
  equity curve iz sačuvanog izveštaja.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Vraća presudu, sve metrike, i obrazloženje. `POST /api/quant/integrity/backtest/{id}` pokreće istu
analizu na završenom backtest-u koji posedujete.

## Zašto je pouzdano

Statistika su čiste funkcije u domen core-u (`Core.Quant`) bez infrastrukturnih
zavisnosti — ne mogu biti srušene mrežnim blipom, i prikačene su golden-vector unit
test-ovima nasuprot objavljenim formulama. Normal CDF/inverse su closed-form aproksimacije
(Abramowitz-Stegun / Acklam), tako da isti input-i uvek daju istu presudu.
