---
title: 0004 — CBotBuilder runs on the web host in a sandbox container
description: Dlaczego niezaufane kompilacje cBot dzieją się na hoście sieciowym wewnątrz jednorazowego kontenera SDK zamiast na węźle.
---

# 0004 — `CBotBuilder` działa na hoście internetowym w kontenerze piaskownicy

## Kontekst

Zbudowanie cBota użytkownika oznacza uruchomienie **niezaufanego MSBuild** — dowolnego kodu w czasie kompilacji (cele, generatory źródeł, skrypty przywracania). Potrzebuje gniazda Docker do obracania kontenera SDK. Węzły uruchamiają kontenery handlujące i nie powinny również posiadać uprawnień kompilacji.

## Decyzja

`CBotBuilder` działa **na hoście sieciowym** (który już ma gniazdo Docker), wewnątrz **jednorazowego kontenera SDK** z:

- katalogiem `/work` z bind-mount (tylko wejścia/wyjścia kompilacji, nie system plików hosta);
- wspólnym wolumenem `app-nuget-cache` dla wydajności przywracania;
- brak dostępu do sieci hosta poza tym, czego przywracanie potrzebuje.

Tak niezaufane MSBuild nie może osiągnąć system plików hosta lub sieci. Kontenery uruchomienia/backtestu, w porównaniu, działają na węzłach wybranych przez `NodeScheduler`.

## Konsekwencje

- Uprawnienie kompilacji (gniazdo Docker) jest ograniczone do hosta sieciowego; węzły uruchamiają tylko dozwolone obrazy handlu.
- Każda kompilacja jest izolowana w jednorazowym kontenerze — złośliwa kompilacja nie może utrwalić się ani uciec.
- Host sieciowy musi mieć dostępne gniazdo Docker; jest to wymaganie wdrażania, nie opcjonalne.
