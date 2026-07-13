---
title: 0001 — Strict DDD with a pure Core
description: Dlaczego logika domeny żyje na agregatach w projekcie Core bez zależności infrastrukturalnych.
---

# 0001 — Strict DDD z czystym `Core`

## Kontekst

Ta aplikacja przenosi rzeczywiste pieniądze. Reguły biznesowe rozsiane po endpointach, usługach w tle i komponentach Razor rozkładają się w nienadające się do testów, niespójne zachowanie — dokładnie tam, gdzie błąd kosztuje użytkownika kapitał.

## Decyzja

Logika domeny żyje **na agregatach, obiektach wartości i usługach domenowych** w `src/Core`, które kompilują się z **zerem zależności infrastrukturalnych** (bez EF, HttpClient, Docker lub ASP.NET). Endpointy, narzędzia MCP, komponenty i `BackgroundService`s **orkiestrują** — nigdy nie decydują. Reguły:

- Brak publicznych setterów; zmiany stanu przez metody ujawniające intencję, które chronią niezmienniki.
- Agregaty odwołują się do siebie przez **silne ID**, nie właściwość nawigacyjną.
- Jeden `SaveChanges` mutuje **jeden** agregat; przepływy między agregatami używają zdarzeń domenowych.
- Prymitywy przecinające granicę domeny są opakowane w obiekty wartości.
- Naruszenia niezmiennika rzucają Core `DomainException`, nie wyjątek frameworku.

## Konsekwencje

- Reguły domeny są testowalne jednostkowo bez bazy danych lub hosta internetowego.
- Czystość `Core` jest wymuszana maszynowo przez `ArchitectureGuardTests` i byłaby nie przebąka kompilacji, gdyby została złamana.
- Jest więcej ceremonii (obiekty wartości, silne ID, zdarzenia domenowe) niż w modelu beztkliwym — jest to celkowy koszt utrzymania reguł przenoszących pieniądze w prawidłowy sposób i w jednym miejscu.
