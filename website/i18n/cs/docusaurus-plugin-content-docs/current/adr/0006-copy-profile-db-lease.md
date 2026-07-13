---
title: 0006 — Hostování kopírování je koordinováno atomátem pronájmu DB
description: Proč jsou profily kopírování nárokovány prostřednictvím atomárního pronájmu Postgres místo vyhrazeného koordinátora a jak to brání dvojitému kopírování.
---

# 0006 — Hostování kopírování je koordinováno atomátem pronájmu DB

## Kontext

Běžící profil kopírování musí být hostován **přesně jedním** uzlem — dva hostitelé na stejném profilu znamená
každý zdrojový obchod je zrcadlen dvakrát (ztráta skutečných peněz). Uzly přicházejí a odcházejí (škálování, selhání, valcování
aktualizace) a nechceme, aby běžela samostatná služba koordinátora.

## Rozhodnutí

Každý `CopyEngineSupervisor` si nárokovává profily s **atomárním pronájmem DB** na tabulce `CopyProfiles`:

- **Tvrzení** — atomární `ExecuteUpdate` (nebo `FOR UPDATE SKIP LOCKED` při limitování na uzel) bere
  profily, které jsou nepřiřazeny *nebo* jejichž pronájem vypršel. Atomicita znamená dva závodící nadřízení
  nikdy nemohou oba tvrdit stejný řádek.
- **Obnovení** — živý uzel obnovuje svůj pronájem každý cyklus, takže si udržuje tvrzení.
- **Nový nárok** — pronájem havarovaného uzlu vyprší a přeživší si profil vezme v následujícím cyklu
  (samo-léčení). Při řádném vypnutí uzel **uvolní** své pronájmy okamžitě, takže převzetí je rychlé.
- **Hlídka** — hostitel, jehož úloha skončila, zatímco profil je stále náš, se restartuje.
- Sladění je náhodně rozptýleno, aby se zabránilo kápání `UPDATE`ů v měřítku.

## Důsledky

- Žádný samostatný koordinátor k nasazení nebo udržování v pořádku — Postgres je jediný zdroj pravdy.
- Dvojitému kopírování se brání atomicitou na úrovni řádku, nikoli aplikací uzamykáním.
- Latence převzetí je ohraničena TTL pronájmu (mínus cesta s řádným uvolněním).
- Toto je peněžní cesta; je chráněna deterministickou stresovou suitu (DST) — nikdy neoslabujte DST
  scénář, aby prošel.
