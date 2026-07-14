---
description: "Každý zobrazený čas je ve vašem časovém pásmu — zjištěném z prohlížeče při první návštěvě a změnitelném v Nastavení. Úložiště a API zůstávají v UTC."
---

# Časové pásmo

Každý čas v aplikaci se zobrazuje ve vašem časovém pásmu, ne serverovém. Vaše volba se uloží do profilu a následuje vás napříč zařízeními.

Při první návštěvě aplikace automaticky převezme pásmo vašeho prohlížeče. Kdykoli jej změníte v Nastavení → Časové pásmo; výchozí pro nasazení je white-label možnost App:Branding:DefaultTimeZone (výchozí UTC). Časy se vždy ukládají a vrací z API v UTC — převádí se jen zobrazení.

- Pořadí určení: pásmo profilu, poté cookie, poté výchozí nasazení, poté UTC.
- Detekce proběhne jednou a nikdy nepřepíše pásmo, které jste zvolili.
- Formátování se řídí vaším jazykem; relativní popisky jako „před 2 minutami“ nejsou dotčeny.
