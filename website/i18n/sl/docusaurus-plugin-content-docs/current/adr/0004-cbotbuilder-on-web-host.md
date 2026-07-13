---
title: 0004 — CBotBuilder teče na spletnem gostitelju v peskovniku
description: Zakaj se nezaupani cBot gradnja zgodi na spletnem gostitelju v enkratni SDK posodi, ne pa na vozlišču.
keywords: CBotBuilder, peskovnik, varnost, Docker, cBot
---

# 0004 — `CBotBuilder` teče na spletnem gostitelju v peskovniku

## Kontekst

Gradnja cBota uporabnika pomeni izvajanje **nezaupnega MSBuild** — poljubne kode pri času gradnje (target,
generator virov, restore skripta). Potrebuje soket Docker, da zasuka SDK kontejner. Vozlišča
tečejo trgovalske kontejnere in ne bi smela imeti tudi privilegijev gradnje.

## Odločitev

`CBotBuilder` teče **na spletnem gostitelju** (ki že ima soket Docker), znotraj **enkratne SDK
posode** z:

- imenom z vezanim `/work` direktorije (samo vhodi/izhodih gradnje, ne gostiteljev datotečnega sistema);
- deljeno glasnostjo `app-nuget-cache` za zmogljivost restore;
- brez dostopa do gostitelja omrežja zunaj tega, kar restore potrebuje.

Torej nezaupni MSBuild ne more doseči gostitelja datotečnega sistema ali omrežja. Posode za teke/backtest, nasprotno,
tečejo na vozlišči, ki ga izberejo `NodeScheduler`.

## Posledice

- Privilegij gradnje (soket Docker) je omejen na spletni gostitelj; vozlišča samo tečejo dovoljene trgovalne slike.
- Vsaka gradnja je izolirana v zavržljivo posodo — zlonamenski graditi se ne more ohraniti ali pobegniti.
- Spletni gostitelj mora imeti dostopni soket Docker; to je zahteva za postavitev, ne pa neobvezno.
