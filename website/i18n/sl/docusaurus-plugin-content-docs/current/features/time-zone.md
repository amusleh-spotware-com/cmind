---
description: "Vsak prikazani čas je v vašem časovnem pasu — zaznanem iz brskalnika ob prvem obisku in spremenljivem v Nastavitvah. Shramba in API-ji ostanejo v UTC."
---

# Časovni pas

Vsak čas, ki ga prikaže aplikacija, je izrisan v vašem časovnem pasu, ne strežniškem. Vaša izbira se shrani v profil in vas spremlja med napravami.

Ob prvem obisku aplikacija samodejno prevzame pas vašega brskalnika. Kadar koli ga spremenite v Nastavitve → Časovni pas; privzeta vrednost namestitve je white-label možnost App:Branding:DefaultTimeZone (privzeto UTC). Časi se vedno shranijo in vrnejo prek API v UTC — pretvori se le prikaz.

- Vrstni red določanja: pas profila, nato piškotek, nato privzeta namestitve, nato UTC.
- Zaznavanje se izvede enkrat in nikoli ne prepiše pasu, ki ste ga izbrali.
- Oblikovanje sledi vašemu jeziku; relativne oznake kot »pred 2 minutama« niso prizadete.
