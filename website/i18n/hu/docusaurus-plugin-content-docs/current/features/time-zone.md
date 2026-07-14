---
description: "Minden megjelenített idő az Ön saját időzónájában jelenik meg — az első látogatáskor a böngészőből felismerve, a Beállításokban módosítható. A tárolás és az API-k UTC-ben maradnak."
---

# Időzóna

Az alkalmazás minden megjelenített ideje az Ön saját időzónájában jelenik meg, nem a szerverében. A választás a profiljába mentődik, és eszközök között is követi Önt.

Az első látogatáskor az alkalmazás automatikusan átveszi a böngésző időzónáját. Bármikor módosíthatja a Beállítások → Időzóna alatt; a telepítés alapértelmezése a white-label App:Branding:DefaultTimeZone beállítás (alapértelmezés UTC). Az időpontok mindig UTC-ben tárolódnak és térnek vissza az API-ból — csak a megjelenítés konvertálódik.

- Feloldási sorrend: profil időzónája, majd a süti, majd a telepítés alapértelmezése, majd UTC.
- A felismerés egyszer fut le, és soha nem írja felül az Ön által választott zónát.
- A formázás a nyelvét követi; a relatív címkék, mint „2 perce“, nem érintettek.
