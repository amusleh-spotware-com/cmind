---
description: "Backtest Integrity Lab — statistiche di overfitting deterministiche, di livello istituzionale (Probabilistic & Deflated Sharpe, t-stat) che trasformano un raw backtest in un verdetto Robust / Fragile / Overfit, correggendo per quante configurazioni hai provato."
---

# Backtest Integrity Lab

Le piattaforme retail ti mostrano lo Sharpe o il net profit di un backtest e si fermano lì. Le istituzioni
non si fidano mai di un raw backtest — chiedono se il risultato sopravvive alla **correzione per selection
bias e il numero di configurazioni provate**. Il Backtest Integrity Lab porta quel controllo in cMind.
È **matematica deterministica** (no AI, no chiamate esterne), quindi il verdetto è riproducibile e ogni
numero è spiegabile.

Aprilo su **cBots → Integrity** (`/quant/integrity`).

## Cosa calcola

Data una serie di rendimenti (o una equity/balance curve) e il numero di param set che hai provato per
arrivarci, l'analizzatore riporta:

- **Sharpe ratio** — per-period e annualizzato (square-root-of-time).
- **Probabilistic Sharpe Ratio (PSR)** — la confidenza che il *vero* Sharpe batta il benchmark,
  tenendo conto della lunghezza della track record, skewness e kurtosis (Bailey & López de Prado, 2012).
  Una record corta o fat-tailed lo abbassa.
- **Deflated Sharpe Ratio (DSR)** — PSR misurato contro un **deflated benchmark**: lo Sharpe che
  ti aspetteresti dal *miglior di N trial random* sotto il null (il False Strategy Theorem). Più
  configurazioni hai provato, più alta è la soglia — questo è ciò che cattura l'overfitting.
- **t-statistic** del rendimento medio. Seguendo Harvey, Liu & Zhu, un edge genuino dovrebbe superare **t ≥ 3.0**,
  non il textbook 2.0.
- **Skewness / kurtosis** dei rendimenti, che alimentano le correzioni PSR/DSR.

## Il verdetto

| Verdetto | Significato | Regola |
|---|---|---|
| **Robust** | L'edge sopravvive ai trial che hai eseguito. | DSR ≥ 95% **and** PSR ≥ 95% **and** |t| ≥ 3.0 |
| **Fragile** | Statisticamente vivo ma non convincente — non sizing up da solo. | tra i due |
| **Overfit** | Molto probabilmente un artefatto del selection bias, non un edge reale. | DSR < 90% |

Ogni risultato porta una rationale in inglese semplice così il "perché" non è mai nascosto.

## Probability of Backtest Overfitting (across trials)

Dare un *count* dei trial è buono; dare l'**effettiva serie out-of-sample di ogni configurazione che hai
provato** è meglio. Incollali nell'opzionale **trial grid** (una serie per riga) e cMind esegue
**Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu, 2015): divide
le osservazioni in gruppi, e per ogni modo di scegliere metà come in-sample sceglie la configurazione
in-sample migliore e verifica se quel vincitore finisce nel bottom half **out-of-sample**. La
**Probability of Backtest Overfitting (PBO)** è la frazione di split dove il vincitore non è riuscito a
generalizzare. Un PBO vicino a 0 significa che la configurazione migliore è genuinamente la migliore; un
PBO di 0.5 o più significa che il tuo processo di selezione sta scegliendo rumore — il verdetto diventa
**Overfit** indipendentemente da quanto buono sembrava il vincitore.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Quando l'optimizer nativo cTrader Console atterrerà, cMind alimenterà qui automaticamente la sua
superficie trial completa.

## Trial — il numero che conta

`Trials` è **quanti param set hai testato** prima di scegliere questo. Testare una strategia e testare
diecimila e tenere la migliore sono cose enormemente diverse: la seconda fabbrica uno Sharpe in-sample
alto per caso. Alimentare l'onesto trial count è il punto centrale — alza la deflation e può spostare
un "grande" backtest a **Overfit**. Quando l'optimizer nativo cTrader Console atterrerà, cMind lo
alimenta automaticamente con la dimensione reale della griglia dello sweep.

## Input

- **Periodic returns** — un numero per periodo (es. `0.01` = +1%). Almeno due.
- **Equity / balance curve** — cMind ricava i rendimenti semplici consecutivi per te.
- Oppure eseguilo direttamente su un backtest completato: `POST /api/quant/integrity/backtest/{instanceId}`
  legge l'equity curve del report memorizzato.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Restituisce il verdetto, tutte le metriche e la rationale. `POST /api/quant/integrity/backtest/{id}` esegue
la stessa analisi su un backtest completato che possiedi.

## Perché è affidabile

Le statistiche sono funzioni pure nel domain core (`Core.Quant`) con zero dipendenze infrastructure
— non possono essere buttate giù da un blip di rete, e sono pinned da golden-vector unit
test contro le formule pubblicate. Le closed-form approximation del normal CDF/inverse
(Abramowitz-Stegun / Acklam), così gli stessi input producono sempre lo stesso verdetto.
