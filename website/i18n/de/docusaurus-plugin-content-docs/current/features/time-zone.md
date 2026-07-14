---
description: "Jede angezeigte Zeit erscheint in Ihrer eigenen Zeitzone — beim ersten Besuch aus dem Browser erkannt und in den Einstellungen änderbar. Speicherung und APIs bleiben UTC."
---

# Zeitzone

Jede in der App angezeigte Zeit wird in Ihrer eigenen Zeitzone dargestellt, nicht der des Servers. Ihre Auswahl wird im Profil gespeichert und folgt Ihnen über Geräte hinweg.

Beim ersten Besuch übernimmt die App automatisch die Zone Ihres Browsers. Sie können sie jederzeit unter Einstellungen → Zeitzone ändern; die Bereitstellungs-Vorgabe ist die White-Label-Option App:Branding:DefaultTimeZone (Standard UTC). Zeiten werden immer in UTC gespeichert und von der API zurückgegeben — nur die Anzeige wird umgerechnet.

- Auflösungsreihenfolge: Profil-Zone, dann Cookie, dann Bereitstellungs-Vorgabe, dann UTC.
- Die Erkennung läuft einmal und überschreibt niemals eine von Ihnen gewählte Zone.
- Die Formatierung folgt Ihrer Sprache; relative Angaben wie „vor 2 Minuten“ sind nicht betroffen.
