---
description: "Institucijski položaj dimenzijo za maloprodaja — volatility ciljni in delni-Kelly izpostavljenost za a ena strategija, plus obratna-volatility tveganja-parnost alokacija z a korelacija matrika čez a knjiga od strategije."
---

# Položaj dimenzijo & portfelj

"Kako velika bi morala biti ta trgovanja?" je na vprašanje, ki odloči ali a rob sestavi ali puhla gor.
Institucije odgovori ga z **volatility ciljni** in na **Kelly kriterij**, in oni graditi a knjiga
z **tveganja parnost** preč kot enak dolarjev. cMind nosi oboje do maloprodaja — deterministična matematika na a
strategija-ov povračilo niz, z a golo-Slovenščina priporočilo.

Odprite **cBots → Položaj dimenzijo** (`/quant/sizing`).

## Ena-strategija dimenzijo

Glede na a strategija-ov povračil (ali kapitala krivulja), a cilj letni volatility, a Kelly frakcija in a
vzvod pokrajina, na sizer poroča:

- **Realizacija letni volatility** — na strategija-ov lastne volatility, leto od na kvadrat-koren-od-časa
  pravilo.
- **Volatility-cilj dimenzijo** — na izpostavljenost, ki naredi realizacija volatility sestanek tvoj cilj
  (`cilj ÷ realizacija vol`), kapljico na tvoj vzvod omejitev. Nižje-vol strategije zaslužijo več velikost.
- **Polni Kelly** — na rast-optimalnega frakcija `f* = μ / σ²` (srednja čez varianca od na povračil).
- **Delni Kelly** — `f*` lestvica z tvoj Kelly frakcija. Pol-Kelly (0.5) je na pogost varna izbira;
  polni Kelly je slavno preveč agresivna za pravi, negotov robovi.
- **Priporočeno izpostavljenost** — na **manjša** (varnejše) od na volatility-cilj in delni-Kelly
  dimenzije, kapljico. A strategija z brez pozitivna rob (polni Kelly ≤ 0) je dimenzionirana do **nič**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Portfelj alokacija

Daj ga dva ali več strategije (poravnana povračilo niz) in ga gradi a knjiga z **obratna-volatility
tveganja parnost** — vsak strategija ponderiran z `1 / volatility`, normaliziran — tako tveganja, ni dolarjev, je deliti
enakomerno. To tudi vrne:

- na **korelacija matrika** čez tvoj strategije (odkrij ones, ki so skrivno na isti del);
- na **projicirani portfelj volatility** pri tiste teža, iz na vzorec kovariance;
- a **vzvod** dejavnik, ki lestvice na celo knjiga proti tvoj cilj volatility (kapljico).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Zakaj je zanesljivo
