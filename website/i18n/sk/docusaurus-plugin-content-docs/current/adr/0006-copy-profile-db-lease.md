---
title: 0006 — Hostovanie kópií je koordinované atomickým leasom DB
description: Prečo sú profily kópií nárokovány cez atomický Postgres lease namiesto špecializovaného koordinátora a ako to bráni dvojitému kopírovaniu.
---

# 0006 — Hostovanie kópií je koordinované atomickým leasom DB

## Kontext

Spustený profil kópie musí byť hostovaný **presne jedným** uzlom — dvaja hostitelia na rovnakom profile znamenajú
každý zdrojový obchod sa zrkadlí dvakrát (skutočné peniaze stratené). Uzly prichádzajú a odchádzajú (škálovanie, havárie, valivé
aktualizácie) a nechceme, aby existovala samostatná služba koordinátora, ktorá by bežala a udržiavala sa.

## Rozhodnutie

Každý `CopyEngineSupervisor` nárokovuje profily s **atomickým DB leasom** na tabuľke `CopyProfiles`:

- **Claim** — atomické `ExecuteUpdate` (alebo `FOR UPDATE SKIP LOCKED` pri limitovaní na uzol) trvá
  profily, ktoré nie sú pridelené *alebo* ktorých lease vypršal. Atomicita znamená dva závodiacich supervisorov
  nikdy oba nárokujú rovnaký riadok.
- **Renew** — aktívny uzol obnovuje svoj lease každý cyklus, takže si ponecháva svoj nárok.
- **Reclaim** — lease zaveseného uzla vyprší a prežívajúci si profil vyzdvihne na svojom ďalšom cykle
  (samoistiace). Pri milostivom vypnutí uzol **uvoľní** svoje leasy okamžite, aby bolo failover rýchle.
- **Watchdog** — hostiteľ, ktorého úloha sa ukončila, kým je profil stále náš, sa reštartuje.
- Rekoncilácia je s rozstupom, aby sa zabránilo davu hrmiacich `UPDATE`ov v rozsahu.

## Dôsledky

- Žiadny samostatný koordinátor na nasadenie alebo udržiavanie zdravý — Postgres je jediný zdroj pravdy.
- Dvojité kopírovanie je bránené atomicitou na úrovni riadkov, nie aplikačnou zámkou.
- Latencia failovera je obmedzená leasom TTL (mínus cesta rýchleho milostivého uvoľnenia).
- Toto je cesta peňazí; je chránená determinickým testom stresu (DST) — nikdy neoslabujte scenár DST, aby prešiel.
