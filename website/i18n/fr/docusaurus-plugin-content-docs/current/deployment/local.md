---
title: Exécutez-le localement
description: Obtenez cMind fonctionnant sur votre propre machine en quelques minutes avec Docker Compose (ou .NET Aspire pour le développement).
sidebar_position: 1
---

# Exécutez cMind localement 🖥️

C'est la façon la plus rapide de voir cMind pour de vrai — une instance complète sur votre propre machine. Prenez un café ;
vous serez probablement connecté avant qu'il refroidisse.

:::tip[Ce que vous aurez à la fin]
Une app web en cours d'exécution à **localhost:8080**, un serveur MCP à **localhost:8081**, une base de données Postgres,
et un nœud travail local prêt à construire et backtester les cBots. Tout sur votre machine, tout à vous.
:::

**Avant de commencer, vous avez besoin de l'un des éléments suivants :**

- **Juste Docker** → utilisez Option A (aucun SDK .NET requis). Recommandé pour un premier regard.
- **.NET 10 SDK + Docker** → utilisez Option B si vous voulez bricoler le code.

Les deux chemins sont cross-platform (Windows / macOS / Linux).

## Option A — Docker Compose (aucun SDK .NET requis)

Prereq : Docker Desktop (ou Docker Engine + plugin compose).

```bash
cp .env.example .env        # modifiez PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- UI Web : <http://localhost:8080> (connectez-vous avec le propriétaire de `.env` ; forcé à changer le mot de passe à la première connexion).
- Serveur MCP : <http://localhost:8081/mcp>.
- Les données Postgres persistent dans le volume `pgdata` ; le schéma migre automatiquement au démarrage.

Le conteneur Web monte le socket Docker de l'hôte (`/var/run/docker.sock`) de sorte que le constructeur in-browser et le **LocalNode** ensemencé
construisent + exécutent les conteneurs cTrader Console sur votre machine.

**Notes cross-platform**
- Docker Desktop (Windows/macOS) expose le socket à `/var/run/docker.sock` — le mount compose fonctionne tel quel.
- Linux : assurez-vous que votre utilisateur peut accéder au socket, ou exécutez compose avec des privilèges suffisants.
- L'image Web est `linux/amd64` ; sur Apple Silicon Docker l'exécute sous émulation.

Arrêtez et essuyez :

```bash
docker compose down          # garder les données
docker compose down -v       # aussi supprimer le volume de la BD
```

## Option B — .NET Aspire (pour le développement)

Prereq : SDK .NET 10 + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire orchestre Postgres, Web, MCP, pgAdmin ; câble les chaînes de connexion + OTLP ; ouvre le dashboard. Définissez les identifiants du propriétaire en tant que paramètres Aspire (`OwnerEmail`, `OwnerPassword`).

Exécutez juste l'app web contre Postgres existante :

```bash
dotnet run --project src/Web
```

## Ajouter des nœuds travail localement

Le LocalNode ensemencé exécute déjà le travail sur votre machine. Pour exercer la **découverte automatique** localement, démarrez l'agent de nœud
pointant vers l'app Web (voir [découverte de nœud](../operations/node-discovery.md)) avec `NodeAgent:MainUrl=http://host.docker.internal:8080`
et `JoinToken` correspondant.

## Dépannage 🔧

Docker a des opinions. Voici les suspects usuels :

| Symptôme | Cause probable & correctif |
|---|---|
| `port is already allocated` sur 8080/8081 | Quelque chose d'autre utilise le port. Arrêtez-le, ou changez le mappage dans `docker-compose.yml`. |
| Web démarre mais les builds/backtests échouent | Le socket Docker n'est pas monté ou accessible. Sur Linux, assurez-vous que votre utilisateur peut atteindre `/var/run/docker.sock`. |
| `permission denied` sur le socket (Linux) | Ajoutez votre utilisateur au groupe `docker` (`sudo usermod -aG docker $USER`) et reconnectez-vous, ou exécutez avec des privilèges suffisants. |
| Première exécution très lente | La première construction tire les images et compile — les exécutions suivantes sont beaucoup plus rapides. Sur Apple Silicon l'image web `linux/amd64` s'exécute sous émulation. |
| Impossible de se connecter | Vérifiez `OWNER_EMAIL` / `OWNER_PASSWORD` dans votre `.env`. La première connexion force un changement de mot de passe. |
| Bizarrerie de la BD après les mises à jour | `docker compose down -v` efface le volume pour un état propre (vous perdrez les données locales). |

Toujours bloqué ? [Ouvrez une Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) — nous sommes
amical. Prochaine étape : [déployer pour de vrai →](./cloud.md)
