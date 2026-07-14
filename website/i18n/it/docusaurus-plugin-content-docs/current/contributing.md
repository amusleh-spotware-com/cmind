---
slug: /contributing
title: Contribuire
description: Come contribuire a cMind — i PR assistiti da umani o AI sono benvenuti. Primo contributo in 10 minuti.
sidebar_position: 5
---

# Contribuire a cMind

Grazie per essere qui. cMind migliora ogni volta che qualcuno apre un problema, segnala un comportamento cTrader preciso, corregge un errore di battitura in questi stessi documenti o spedisce un PR. **Non è necessario essere un mago di .NET** — i tester, i trader e i riparatori di doc sono apprezzati quanto le persone che scrivono aggregati.

:::tip[La guida canonica vive nel repo]
Questa pagina è la rampa di accesso amichevole. Il processo completo e sempre attuale — regole fondamentali, convenzioni di codifica, flusso di revisione — è in **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Il tuo primo contributo in ~10 minuti

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 warnings, o CI cortesemente ti rifiuterà
dotnet test           # unit + integration + E2E
```

Trovato qualcosa da correggere? Fai un branch, cambialo, aggiungi un test e apri un PR. Questo è l'intero loop.

## Modi di aiutare (non tutti sono codice)

| Contributo | Sforzo | Dove |
|---|---|---|
| Segnala un bug riproducibile | 10 min | [Segnalazione bug](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| Suggerisci una funzione | 10 min | [Richiesta di funzione](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| Migliora questi documenti | 15 min | Modifica sotto `website/docs/` e PR |
| Aggiungi un test mancante | 30 min | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| Segnala il comportamento esatto di cTrader | 10 min | [Apri una discussione](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Le regole della casa (versione breve)

cMind sposta **denaro reale**, quindi alcune cose sono non negoziabili — e sinceramente, rendono la base di codice una gioia in cui lavorare:

- **Domain-Driven Design rigoroso.** La logica di business risiede su aggregati e value object, mai su endpoint o UI. (C'è un playbook amichevole per questo nel repo.)
- **Tre test tier, ogni cambiamento.** Unit + integration + E2E, *inclusi* percorsi di errore (connessioni interrotte, ordini rifiutati, nodi morti). I test verdi sono il prezzo di ammissione.
- **Zero avvisi.** `TreatWarningsAsErrors=true`. Idiomi C# 14 moderni.
- **Nessun segreto, nessuna stringa magica, mai `DateTime.UtcNow`** (inietta `TimeProvider` invece).
- **Documenti nello stesso commit.** Cambia il comportamento → aggiorna il suo doc. Sì, questo include questo sito.

Dettagli completi, con il *perché* dietro ogni regola, in [CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) e [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Contribuire con l'AI

Accogliamo genuinamente i **PR assistiti da AI** — questo progetto è costruito per essere lavorato da agenti così come umani. Se stai guidando Claude, Copilot o simile: puntalo su [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), lascialo leggere i file `CLAUDE.md` annidati e mantienilo allo stesso livello (test, zero avvisi, DDD). Un buon PR AI è indistinguibile da un buon PR umano — stessa revisione, stessa accoglienza.

## Sii eccellente con gli altri

Abbiamo un [Codice di condotta](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md). L'essenza: sii gentile, assumi buona fede e ricorda che c'è una persona (o un agente di una persona) dall'altra parte. Fai domande presto — è una forza, non un fastidio.

Benvenuto a bordo. Non vediamo l'ora di vedere cosa costruisci.
