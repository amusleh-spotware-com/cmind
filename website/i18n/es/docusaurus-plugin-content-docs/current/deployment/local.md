---
title: Ejecútalo localmente
description: Obtén cMind ejecutándose en tu propia máquina en unos minutos con Docker Compose (o .NET Aspire para desarrollo).
sidebar_position: 1
---

# Ejecuta cMind localmente 🖥️

Esta es la forma más rápida de ver cMind de verdad — una instancia completa en tu propia máquina. Toma un café;
probablemente hayas iniciado sesión antes de que se enfríe.

:::tip[Lo que tendrás al final]
Una aplicación web en ejecución en **localhost:8080**, un servidor MCP en **localhost:8081**, una base de datos Postgres,
y un nodo de trabajo local listo para compilar y hacer backtest de cBots. Todo en tu máquina, todo tuyo.
:::

**Antes de empezar, necesitas uno de:**

- **Solo Docker** → usa Opción A (sin SDK de .NET requerido). Recomendado para una primera mirada.
- **.NET 10 SDK + Docker** → usa Opción B si deseas hackear el código.

Ambas rutas son multiplataforma (Windows / macOS / Linux).

## Opción A — Docker Compose (sin SDK de .NET requerido)

Requisito previo: Docker Desktop (o Docker Engine + plugin compose).

```bash
cp .env.example .env        # edita PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Interfaz web: <http://localhost:8080> (inicia sesión con propietario desde `.env`; fuerza cambio de contraseña en el primer inicio de sesión).
- Servidor MCP: <http://localhost:8081/mcp>.
- Los datos de Postgres persisten en el volumen `pgdata`; el esquema se migra automáticamente al iniciar.

El contenedor web monta el socket Docker del host (`/var/run/docker.sock`) para que el compilador en el navegador y el **LocalNode** sembrado construyan + ejecuten contenedores de cTrader Console en tu máquina.

**Notas multiplataforma**
- Docker Desktop (Windows/macOS) expone socket en `/var/run/docker.sock` — el montaje de composición funciona como está.
- Linux: asegúrate de que tu usuario pueda acceder al socket, o ejecuta la composición con privilegios suficientes.
- La imagen web es `linux/amd64`; en Apple Silicon Docker la ejecuta bajo emulación.

Detener y limpiar:

```bash
docker compose down          # mantener datos
docker compose down -v       # también eliminar el volumen de base de datos
```

## Opción B — .NET Aspire (para desarrollo)

Requisito previo: SDK de .NET 10 + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire orquesta Postgres, Web, MCP, pgAdmin; cables de cadenas de conexión + OTLP; abre panel. Establece credenciales del propietario como parámetros de Aspire (`OwnerEmail`, `OwnerPassword`).

Ejecuta solo la aplicación web contra Postgres existente:

```bash
dotnet run --project src/Web
```

## Agregar nodos de trabajo localmente

LocalNode sembrado ya ejecuta trabajo en tu máquina. Para ejercer **auto-descubrimiento** localmente, inicia el agente de nodos apuntando a la aplicación web (ver [descubrimiento de nodos](../operations/node-discovery.md)) con `NodeAgent:MainUrl=http://host.docker.internal:8080` y `JoinToken` coincidente.

## Solución de problemas 🔧

Docker tiene opiniones. Aquí están los sospechosos habituales:

| Síntoma | Probable causa y solución |
|---|---|
| `port is already allocated` en 8080/8081 | Algo más está utilizando el puerto. Detenlo o cambia el mapeo en `docker-compose.yml`. |
| La web se inicia pero compilaciones/backtests fallan | El socket Docker no está montado o accesible. En Linux, asegúrate de que tu usuario pueda llegar a `/var/run/docker.sock`. |
| `permission denied` en el socket (Linux) | Agrega tu usuario al grupo `docker` (`sudo usermod -aG docker $USER`) y vuelve a iniciar sesión, o ejecuta con privilegios suficientes. |
| Primera ejecución muy lenta | La compilación inicial extrae imágenes y compila — las ejecuciones posteriores son mucho más rápidas. En Apple Silicon, la imagen web `linux/amd64` se ejecuta bajo emulación. |
| No puedo iniciar sesión | Verifica `OWNER_EMAIL` / `OWNER_PASSWORD` en tu `.env`. El primer inicio de sesión fuerza un cambio de contraseña. |
| Extrañeza de la base de datos después de actualizaciones | `docker compose down -v` limpia el volumen para una pizarra limpia (perderás datos locales). |

¿Aún atrapado? [Abre una discusión](https://github.com/amusleh-spotware-com/cmind/discussions) — somos
amigables. Siguiente parada: [despliegúealo para verdad →](./cloud.md)
