---
title: 0006 — A másolási üzemeltetés egy atomi DB-bérleti szerződéssel koordinálódik
description: Miért az másolási profilok egy atomi Postgres-bérleti szerződésen keresztül igényelnek egy dedikált koordinátor helyett, és hogyan akadályozza meg a kettős másolást.
---

# 0006 — A másolási üzemeltetés egy atomi DB-bérleti szerződéssel koordinálódik

## Kontextus

A futó másolási profilot **pontosan egy** node-nak kell üzemeltetnie — két gép ugyanazon a profilon azt jelenti, hogy minden forrás-kereskedelem kétszer tükrözödik (valódi pénz veszett el). A node-ok jönnek és mennek (méretezés, összeomlások, folyamatos frissítések), és nem akarunk egy külön koordinátor-szolgáltatást futtatni és életben tartani.

## Döntés

Minden `CopyEngineSupervisor` profilokat igényel egy **atomi DB-bérleti szerződéssel** a `CopyProfiles` táblában:

- **Igénylés** — egy atomi `ExecuteUpdate` (vagy `FOR UPDATE SKIP LOCKED` a node-onkénti korlátozásnál) olyan profilokat vesz fel, amelyek nem rendeltek vagy amelyek bérleti szerződése lejárt. Az atomicitás azt jelenti, hogy két versengő felügyelő soha nem igényli ugyanazt a sort.
- **Megújítás** — egy élő node minden ciklus során felújítja a bérleti szerződését, így megtartja az igénylést.
- **Újra-igénylés** — egy összeomlott node bérleti szerződése lejár, és egy túlélő a profilt feloldja a következő cikluson (öngyógyulás). Szlenderű leállításnál a node **azonnal felszabadítja** a bérleti szerződéseket, így a feladatátvétel gyors.
- **Őr** — egy olyan gép, amelynél a feladat kilépett, míg a profil még az enyénk, újraindul.
- Az egyeztetés szóródott, hogy elkerüljük a frissítések dörgedelmet az `UPDATE`-nél a méretezésnél.

## Következmények

- Nincs önálló koordinátor, amelyet telepíteni vagy egészségesnek kell tartani — a Postgres az igazság egyedüli forrása.
- A kettős másolást a sor-szintű atomicitás, nem az alkalmazás-szintű zárolás akadályozza meg.
- A feladatátvétel késleltetése a bérleti szerződés TTL-je által van korlátozva (mínusz a gyors-útvonal szlendes kibocsátása).
- Ez a pénz útja; a determinisztikus stressz-szoftverrel (DST) őrzik — soha ne gyengítsen egy DST-forgatókönyvet, hogy átmenjen.
