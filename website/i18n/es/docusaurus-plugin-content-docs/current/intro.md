---
slug: /intro
title: Bienvenido a cMind
description: Una introducción amena a cMind — la plataforma de operaciones de trading para cTrader, de código abierto y autoalojable.
sidebar_position: 1
---

# Bienvenido a cMind 👋

:::warning[Software en fase Alfa — no está listo para producción]
cMind está en desarrollo activo. Espera aristas sin pulir, cambios que rompen la compatibilidad entre versiones y funciones todavía en progreso. **Necesitamos testers de la comunidad, reportadores de bugs y colaboradores tempranos** que ayuden a darle forma. Si te encuentras un problema, [repórtalo](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) — tus comentarios del mundo real son lo más valioso que puedes aportar ahora mismo.
:::

Así que quieres crear bots de trading, hacerles backtesting sin fundir tu portátil, ejecutarlos en
varias máquinas, replicar operaciones en una docena de cuentas y que una IA vigile el riesgo mientras
duermes. **Estás justo en el lugar correcto.**

cMind es una **plataforma de operaciones de trading para cTrader, de código abierto y autoalojable**.
Piénsalo como tu mesa de trading completa — creación, ejecución, una flota de cómputo, copy trading y
un núcleo de IA — todo en una aplicación tranquila, oscura y adaptada a móviles que te pertenece de
principio a fin.

:::tip[En una frase]
Crea → backtest → ejecuta → copia tus estrategias de cTrader a escala, con IA integrada, en tus propios
servidores y bajo tu propia marca.
:::

## ¿Qué puede hacer realmente?

| Quieres… | cMind lo hace | Leer más |
|---|---|---|
| Escribir un cBot en el navegador | IDE Monaco + plantillas C#/Python, compilaciones aisladas | [Compilar y backtest](./features/build-and-backtest.md) |
| Hacer backtesting en varias máquinas | Una flota de nodos autorreparable elige la máquina menos ocupada | [Escalado](./deployment/scaling.md) |
| Copiar una cuenta en muchas | Replicación robusta con resincronización, sin operaciones duplicadas | [Copy trading](./features/copy-trading.md) |
| Que la IA haga el trabajo pesado | Generación de estrategias, autorreparación, guardián de riesgo, análisis posmortem | [Núcleo de IA](./features/ai.md) |
| Cumplir las reglas de la prop firm | Seguimiento de equidad en vivo + simulación de reglas de desafío | [Prop-firm](./features/prop-firm.md) |
| Validar un edge de backtest | Corrección de sobreajuste PSR / DSR / t-stat | [Laboratorio de integridad de backtest](./features/backtest-integrity.md) |
| Entender tus propios hábitos | Detección de fugas de comportamiento + coach de IA | [Diario de trading](./features/trading-journal.md) |
| Seguir eventos macro para una estrategia | Calendario punto-en-tiempo, bloqueo de noticias, API cBot | [Calendario económico](./features/economic-calendar.md) |
| Puntuar la fortaleza macro de divisas | Perspectiva de IA sobre todos los pares | [Fortaleza de divisas](./features/currency-strength.md) |
| Asegurar cuentas con 2FA | App autenticadora TOTP + códigos de respaldo | [Autenticación de dos factores](./features/two-factor-auth.md) |
| Permitir a los propietarios ajustarlo en tiempo de ejecución | Cada opción de marca blanca en vivo en Ajustes → Despliegue | [Ajustes del propietario](./features/white-label-owner-settings.md) |
| Ejecutarlo en cualquier idioma | 23 idiomas incl. RTL — el build falla si falta una clave | [Localización](./features/localization.md) |
| Lanzarlo como *tu* producto | Marca blanca completa: nombre, colores, logotipo, favicon | [Marca blanca](./features/white-label.md) |
| Ejecutarlo en tu teléfono | PWA instalable y adaptada a móviles | [PWA](./features/pwa.md) |
| Controlarlo desde un cliente de IA | Servidor MCP integrado (HTTP + SSE) | [MCP](./features/mcp.md) |

## La ruta de 5 minutos ⏱️

Si tienes Docker y cinco minutos, ahora mismo puedes estar toqueteando una instancia real de cMind:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Luego abre **<http://localhost:8080>**, inicia sesión y listo. El recorrido completo (con solución de
problemas para cuando Docker inevitablemente tenga opiniones) está en
**[Ejecutarlo en local](./deployment/local.md)**.

## ¿Nuevo por aquí? Sigue el camino de baldosas amarillas 🟡

1. **[¿Para quién es esto?](./audience.md)** — asegúrate de que eres de nuestro tipo de problema.
2. **[Ejecutarlo en local](./deployment/local.md)** — pon en marcha una instancia real.
3. **[Funciones](./features/README.md)** — el recorrido completo por lo que hay dentro.
4. **[Desplegar en serio](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Hazlo tuyo](./white-label-for-business.md)** — ponle tu marca blanca para tu negocio.
6. **[Contribuir](./contributing.md)** — los PR (humanos *y* asistidos por IA) son muy bienvenidos.

## Unas palabras rápidas sobre el dinero 💸

cMind mueve **capital real**. Nos lo tomamos en serio — cada cambio se entrega con pruebas unitarias,
de integración y de extremo a extremo, incluidas las rutas de fallo (conexiones caídas, órdenes
rechazadas, nodos muertos). Tú también deberías tomártelo en serio: **prueba primero en una cuenta
demo** y lee las [notas de cumplimiento](./features/compliance.md) antes de apuntarlo a algo real. El
trading es arriesgado; este software es una herramienta, no asesoramiento financiero.

Bien — basta de preámbulos. Vamos a construir algo. →
