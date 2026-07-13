---
title: 0001 — DDD rigoroso con un Core puro
description: Perché la logica del dominio risiede sugli aggregati in un progetto Core con zero dipendenze infrastrutturali.
---

# 0001 — DDD rigoroso con un `Core` puro

## Contesto

Questa app sposta denaro reale. Le regole di business sparse tra endpoint, servizi di sfondo e componenti Razor si deteriorano in comportamenti non testabili e incoerenti — esattamente dove un bug costa capitale a un utente.

## Decisione

La logica del dominio risiede **su aggregati, value object e domain service** in `src/Core`, che si compila con **zero dipendenze infrastrutturali** (niente EF, HttpClient, Docker o ASP.NET). Endpoint, strumenti MCP, componenti e `BackgroundService` **orchestrano** — non decidono mai. Regole:

- Niente setter pubblici; i cambiamenti di stato avvengono attraverso metodi che rivelano l'intenzione e proteggono gli invarianti.
- Gli aggregati si riferiscono l'uno all'altro per **ID forte**, non per proprietà di navigazione.
- Un `SaveChanges` muta **un** aggregato; i flussi tra aggregati usano eventi di dominio.
- I primitivi che attraversano un confine di dominio sono avvolti in value object.
- Le violazioni di invariante lanciano un `DomainException` Core, non un'eccezione del framework.

## Conseguenze

- Le regole del dominio sono testabili a livello di unità senza un database o un host web.
- La purezza di `Core` è applicata meccanicamente dai `ArchitectureGuardTests` e fallirebbe la build se fosse rotta.
- C'è più cerimonia (value object, ID forti, eventi di dominio) rispetto a un modello anemico — questo è il costo deliberato di mantenere le regole di movimento del denaro corrette e in un unico luogo.
