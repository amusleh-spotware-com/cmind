---
title: 0001 — Strict DDD s čistým jadrom
description: Prečo logika domény žije na agregátoch v Core projekte bez závislostí od infraštruktúry.
---

# 0001 — Strict DDD s čistým `Core`

## Kontext

Táto aplikácia pohybuje skutočnými peniazmi. Obchodné pravidlá rozptýlené naprieč koncovými bodmi, vláknami na pozadí a komponentami Razor sa rozpadajú do neotestovateľného a nekonzistentného správania — presne tam, kde chyba stojí používateľovi peniaze.

## Rozhodnutie

Logika domény žije **na agregátoch, objektoch hodnôt a doménových službách** v `src/Core`, ktorá sa kompiluje s **nula infraštruktúrnymi závislosťami** (bez EF, HttpClient, Docker alebo ASP.NET). Koncové body, nástroje MCP, komponenty a `BackgroundService`s **orchestrujú** — nikdy nerozhodujú. Pravidlá:

- Bez verejných nastavovačov; zmeny stavu prostredníctvom metód odhaľujúcich zámer, ktoré chránia invarianty.
- Agregáty sa odkazujú navzájom **silným ID**, nie vlastnosťou navigácie.
- Jeden `SaveChanges` mutuje **jeden** agregát; prietoky medzi agregátmi používajú doménové udalosti.
- Primitíva prekračujúca hranicu domény sú zabalená v objektoch hodnôt.
- Porušenia invariantov vyvolávajú `DomainException` z Core, nie výnimku rámca.

## Dôsledky

- Doménové pravidlá sú jednotkovo testovateľné bez databázy alebo webového hostitela.
- Čistota `Core` je strojovo vynútená testami `ArchitectureGuardTests` a zlyhala by zostavenie, ak by bola porušená.
- Je tu viac ceremoniálu (objekty hodnôt, silné ID, doménové udalosti) ako v anemickom modeli — to je zámyselná cena zachovania správnosti pravidiel pohybujúcich peniaze a ich umiestnenia na jednom mieste.
