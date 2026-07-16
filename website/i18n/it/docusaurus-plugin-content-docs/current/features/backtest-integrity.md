---
description: "Backtest Integrity Lab — statistiche di overfitting di qualità istituzionale, deterministica (Probabilistic & Deflated Sharpe, t-stat) che trasformano un backtest grezzo in un verdetto Robusto / Fragile / Overfitting, corregendo il numero di configurazioni che hai provato."
---

# Backtest Integrity Lab

Le piattaforme retail ti mostrano uno Sharpe o un profitto netto del backtest e si fermano lì. Le istituzioni non si fidano mai di un backtest grezzo — chiedono se il risultato resiste **alla correzione per bias di selezione e al numero di configurazioni provate**. Il Backtest Integrity Lab porta questo controllo a cMind. È **matematica deterministica** (nessuna IA, nessuna chiamata esterna), quindi il verdetto è riproducibile e ogni numero è spiegabile.

Aprilo su **cBots → Integrity** (`/quant/integrity`).

## Cosa calcola

Data una serie di rendimenti (o una curva di equity/bilancio) e il numero di set di parametri che hai provato per arrivarci, l'analizzatore riporta:

- **Sharpe ratio** — per periodo e annualizzato (radice quadrata del tempo).
- **Probabilistic Sharpe Ratio (PSR)** — la confidenza che lo Sharpe *vero* batta il benchmark, considerando la lunghezza dello storico, l'asimmetria e la curtosi (Bailey & López de Prado, 2012). Uno storico breve o con code grasse lo abbassa.
- **Deflated Sharpe Ratio (DSR)** — PSR misurato rispetto a un **benchmark deflazionato**: lo Sharpe che ti aspetteresti dal *migliore dei N trial casuali* sotto l'ipotesi nulla (il False Strategy Theorem). Più configurazioni hai provato, più alta la soglia — questo è ciò che cattura l'overfitting.
- **t-statistic** della media dei rendimenti. Seguendo Harvey, Liu & Zhu, un vero edge dovrebbe superare **t ≥ 3.0**, non il classico 2.0.
- **Skewness / kurtosis** dei rendimenti, che alimentano le correzioni PSR/DSR.

## Il verdetto

| Verdetto | Significato | Regola |
|---|---|---|
| **Robusto** | L'edge resiste ai trial che hai eseguito. | DSR ≥ 95% **e** PSR ≥ 95% **e** \|t\| ≥ 3.0 |
| **Fragile** | Statisticamente vivo ma non in modo convincente — non aumentare la dimensione basandoti solo su questo. | tra i due |
| **Overfitting** | Molto probabilmente un artefatto del bias di selezione, non un vero edge. | DSR < 90% |

Ogni risultato porta una spiegazione in linguaggio semplice in modo che il "perché" non sia mai nascosto.

## Probabilità di Backtest Overfitting (tra i trial)

Fornire un *numero* di trial è buono; fornire la **serie out-of-sample effettiva di ogni configurazione che hai provato** è meglio. Incollale nella griglia di trial opzionale (una serie per riga) e cMind esegue **Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu, 2015): divide le osservazioni in gruppi, e per ogni modo di scegliere metà come in-sample seleziona la migliore configurazione in-sample e verifica se il vincitore finisce nella metà inferiore **out-of-sample**. La **Probability of Backtest Overfitting (PBO)** è la frazione di split dove il vincitore non è riuscito a generalizzare. Una PBO vicina a 0 significa che la migliore configurazione è veramente la migliore; una PBO di 0.5 o più significa che il tuo processo di selezione sta selezionando rumore — il verdetto diventa **Overfitting** indipendentemente da quanto bene appariva il vincitore.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Quando l'optimizer nativo di cTrader Console arriverà, cMind alimenterà automaticamente la sua intera superficie di trial qui.

## Trial — il numero che conta

`Trials` è **quanti set di parametri hai testato** prima di scegliere questo. Testare una strategia e testarne diecimila e mantenere la migliore sono cose selvaggiamente diverse: la seconda produce uno Sharpe in-sample alto per caso. Fornire il numero di trial onesto è l'intero punto — aumenta la deflazione e può spostare un backtest "eccellente" a **Overfitting**. Quando l'optimizer nativo di cTrader Console arriverà, cMind alimenta automaticamente la dimensione della griglia effettiva dello sweep.

## Input

- **Periodic returns** — un numero per periodo (es. `0.01` = +1%). Almeno due. Il campo si valida mentre digiti: conta i numeri validi, contrassegna qualsiasi token che non sia un numero, e abilita **Analyze** solo una volta che sono presenti almeno due valori puliti (la griglia di trial abilita **Assess overfitting** una volta che sono pronte due serie di quattro o più numeri ciascuna).
- **Equity / balance curve** — cMind deriva i rendimenti semplici consecutivi per te.
- **Direttamente da un backtest run — nessun copia-incolla.** Ogni backtest completato espone un'icona shield **Check backtest integrity** sulla riga dell'elenco **Backtest** e nella vista di dettaglio della sua istanza; un clic esegue il Lab sulla curva di equity memorizzata di quel run e mostra il verdetto in una finestra di dialogo. L'icona è disabilitata finché il backtest non è completato e non ha prodotto un report, quindi non è mai un controllo morto. Dietro le quinte questo è `POST /api/quant/integrity/backtest/{instanceId}`, che legge la curva di equity del report memorizzato.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Restituisce il verdetto, tutte le metriche e la spiegazione. `POST /api/quant/integrity/backtest/{id}` esegue la stessa analisi su un backtest completato che possiedi.

## Perché è affidabile

Le statistiche sono funzioni pure nel dominio core (`Core.Quant`) senza dipendenze di infrastruttura — non possono essere abbattute da un problema di rete, e sono ancorate da test unitari golden-vector rispetto alle formule pubblicate. La CDF normale/inversa sono approssimazioni in forma chiusa (Abramowitz-Stegun / Acklam), quindi gli stessi input producono sempre lo stesso verdetto.
