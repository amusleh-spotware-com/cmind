---
title: 0001 — Szigorú DDD egy tiszta Core-ral
description: Miért az üzleti logika az aggregátumok Core projektben található, függetlenül az infrastruktúra függőségektől.
---

# 0001 — Szigorú DDD egy tiszta `Core`-ral

## Kontextus

Ez az alkalmazás valós pénzzel foglalkozik. Az üzleti szabályok szétszóródva az endpointok, background servicek és Razor komponensek között az értékelhetetlen, inkonzisztens viselkedésre hanyatlanak — pontosan olyan hely, ahol egy hiba a felhasználó tőkéjébe kerül.

## Döntés

Az üzleti logika **az aggregátumok, érték objektumok és domain servicek** között él a `src/Core`-ban, amely **nulla infrastruktúra függőséggel** fordítható (nincs EF, HttpClient, Docker vagy ASP.NET). Az endpointok, MCP eszközök, komponensek és `BackgroundService`k **irányítanak** — soha nem döntenek. Szabályok:

- Nincs nyilvános setter; az állapot változtatásai szándékot nyilvánító metódusokon keresztül történnek, amelyek az invariánsokat megóvják.
- Az aggregátumok **erős ID-vel** hivatkoznak egymásra, nem navigációs tulajdonságokkal.
- Egy `SaveChanges` **egy** aggregátumot mutáltat; az aggregátumon felüli folyamatok domain eseményeket használnak.
- A domain határt átlépő primitíveket érték objektumokba csomagoljuk.
- Az invariáns sérelmei Core `DomainException` kivételt dobnak, nem keretrendszer kivételt.

## Következmények

- A domain szabályok tesztelhetők adatbázis vagy webkiszolgáló nélkül.
- A `Core` tisztaság gépezet által érvényesítve van az `ArchitectureGuardTests` által és ha megtörik a build meghiúsul.
- Több ceremónia van (érték objektumok, erős ID-k, domain események) mint egy anem modell — ez az szándékos ár az egyenlegmozgatási szabályok helyes és egy helyen tartásához.
