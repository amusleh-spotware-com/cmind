---
title: Architecture Decision Records
description: Nieoczywiste decyzje projektowe stojące za cMind — kontekst, decyzja i konsekwencje — których nie możesz odczytać z kodu.
---

# Architecture Decision Records

Te dokumenty rejestrują decyzje projektowe, które **nie możesz wywnioskować z kodu** — kompromisy, nieobrane ścieżki i przyczyny. Każdy jest krótki: *Kontekst → Decyzja → Konsekwencje*. Nowa decyzja strukturalna → dodaj tutaj ADR (następny numer), aby następny inżynier (człowiek lub AI) odziedziczył rozumowanie, nie tylko wynik.

| # | Decyzja |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | Ścisłe DDD z czystym `Core` |
| [0002](./0002-tph-instance-replaces-entity.md) | Stan instancji jest TPH; przejście zastępuje encję |
| [0003](./0003-external-nodes-http-jwt.md) | Węzły cTrader CLI to HTTP + JWT, brak SSH/shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` działa na hoście internetowym w kontenerze piaskownicy |
| [0005](./0005-anthropic-raw-http.md) | Klient AI używa surowego HTTP, nie SDK Anthropic |
| [0006](./0006-copy-profile-db-lease.md) | Hosting kopii jest koordynowany przez atomowy leasing bazy danych |
