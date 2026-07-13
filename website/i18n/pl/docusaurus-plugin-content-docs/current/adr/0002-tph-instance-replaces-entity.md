---
title: 0002 — Instance state is TPH; a transition replaces the entity
description: Dlaczego identyfikator instancji zmienia się w trakcie jej cyklu życia i dlaczego identyfikator kontenera jest kluczem stabilnym.
---

# 0002 — Stan instancji jest TPH; przejście zastępuje encję

## Kontekst

Instancja uruchomienia/backtestu przechodzi przez stany (oczekiwanie → zaplanowana → uruchamianie → biegający → terminal). Modelujemy stan za pomocą EF Core **Table-Per-Hierarchy (TPH)**: każdy stan jest podtypem (`StartingRunInstance`, `RunningRunInstance`, …). Kolumna dyskryminatora TPH EF **nie może się zmienić** w istniejącym wierszu.

## Decyzja

Przejście stanu **zastępuje encję** nową instancją podtypu zamiast mutować pole statusu. Ponieważ wiersz jest zastępowany, **identyfikator instancji zmienia się** między uruchamianiem → bieganiem → terminalem. **Identyfikator kontenera jest stabilny** i jest przenoszony przez przejścia; agent węzła HTTP jest zaindeksowany przez identyfikator kontenera dla statusu/raportu/zatrzymania/logów.

## Konsekwencje

- Każdy stan jest odrębnym typem zawierającym tylko pola i metody ważne w tym stanie — nielegalne przejścia i bezsensowny dostęp do pola to błędy kompilacji, nie sprawdzenia w czasie wykonania.
- Osoby dzwoniące **nie powinny** buforować identyfikator instancji między przejściami; używaj identyfikatora kontenera jako stabilnego uchwytu dla wszystkiego, co obejmuje stany.
- Logika przejścia żyje w `InstanceTransitions`; zmiana identyfikatora jest zamierzona, nie błędem.
