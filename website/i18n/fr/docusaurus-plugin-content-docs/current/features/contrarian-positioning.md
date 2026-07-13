---
description: "Positionnement de vente au détail contrarian — transforme le % de traders de détail long en un biais contrarian (estomper la foule quand elle est unilatérale), plus des objets de valeur de signal point-dans-le-temps qui se gardent contre le biais d'anticipation."
---

# Positionnement de vente au détail contrarian

La foule de détail est l'un des rares signaux de sentiment véritablement utiles en FX — en tant qu'indicateur **contrarian**. Quand la grande majorité des traders de détail sont long, le prix a historiquement eu tendance à baisser, et vice-versa. Cet outil transforme le positionnement de la foule en une lecture exploitable.

Ouvrez **cBots → Positionnement Contrarian** (`/quant/positioning`).

## Que fait-il

Entrez le **% de traders de détail long** (à partir de la page de sentiment de votre courtier ou d'un flux tel que FXSSI) et il retourne :

- **Biais contrarian** — **Bearish** quand ≥ 60% sont long (foule trop longue), **Bullish** quand ≤ 40% sont long (foule trop courte), **Neutre** dans la bande d'indécision 40–60%;
- **Force** — à quel point la foule est unilatérale (0 = équilibré, 1 = entièrement unilatéral), pour peser le signal.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Point-dans-le-temps par construction

Sous le capot, la couche de signal (`Core.Signals`) modélise un `PointInTimeSignal` qui est **estampillé avec le moment où il était connaissable** et refuse d'être construit sans lui. Tout backtest ou agent autonome qui consomme un signal vérifie `IsKnownAt(decisionTime)` — afin que les données futures ne puissent jamais fuir dans une décision historique. Le biais d'anticipation est le premier tueur de reproductibilité en finance quant ; le modèle de domaine le rend structurellement impossible.

## Pourquoi c'est fiable

Code de domaine pur et déterministe sans dépendance d'infrastructure — les seuils contrarien et la garde point-dans-le-temps sont testés en unité, y compris les limites 40/60 et le rejet hors gamme.
