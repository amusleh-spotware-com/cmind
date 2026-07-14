---
description: "Chaque heure affichée apparaît dans votre propre fuseau horaire — détecté depuis le navigateur à la première visite et modifiable dans les Paramètres. Le stockage et les API restent en UTC."
---

# Fuseau horaire

Chaque heure affichée par l'application est rendue dans votre propre fuseau horaire, pas celui du serveur. Votre choix est enregistré dans votre profil et vous suit d'un appareil à l'autre.

À votre première visite, l'application adopte automatiquement le fuseau de votre navigateur. Vous pouvez le changer à tout moment dans Paramètres → Fuseau horaire ; la valeur par défaut du déploiement est l'option white-label App:Branding:DefaultTimeZone (UTC par défaut). Les heures sont toujours stockées et renvoyées par l'API en UTC — seul l'affichage est converti.

- Ordre de résolution : fuseau du profil, puis le cookie, puis la valeur par défaut du déploiement, puis UTC.
- La détection s'exécute une fois et ne remplace jamais un fuseau que vous avez choisi.
- Le formatage suit votre langue ; les libellés relatifs comme « il y a 2 minutes » ne sont pas affectés.
