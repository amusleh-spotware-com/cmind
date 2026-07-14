---
description: "Každý zobrazený čas je vo vašom časovom pásme — zistenom z prehliadača pri prvej návšteve a zmeniteľnom v Nastaveniach. Úložisko a API zostávajú v UTC."
---

# Časové pásmo

Každý čas v aplikácii sa zobrazuje vo vašom časovom pásme, nie serverovom. Vaša voľba sa uloží do profilu a nasleduje vás naprieč zariadeniami.

Pri prvej návšteve aplikácia automaticky prevezme pásmo vášho prehliadača. Kedykoľvek ho zmeníte v Nastavenia → Časové pásmo; predvolené pre nasadenie je white-label možnosť App:Branding:DefaultTimeZone (predvolené UTC). Časy sa vždy ukladajú a vracajú z API v UTC — prevádza sa iba zobrazenie.

- Poradie určenia: pásmo profilu, potom cookie, potom predvolené nasadenia, potom UTC.
- Detekcia prebehne raz a nikdy neprepíše pásmo, ktoré ste zvolili.
- Formátovanie sa riadi vaším jazykom; relatívne popisy ako „pred 2 minútami“ nie sú dotknuté.
