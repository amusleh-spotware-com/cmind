---
slug: /contributing
title: Contribuyendo
description: Cómo contribuir a cMind — PR manuales o asistidas por IA bienvenidas. Primera contribución en 10 minutos.
sidebar_position: 5
---

# Contribuyendo a cMind 🛠️

Gracias por estar aquí. cMind mejora cada vez que alguien abre un problema, reporta el comportamiento preciso
de cTrader, arregla una errata en estos documentos, o envía un PR. **No necesitas ser un mago de .NET** — 
probadores, comerciantes y correctores de documentos son tan valorados como los que escriben agregados.

:::tip[La guía canónica vive en el repositorio]
Esta página es la rampa de entrada amigable. El proceso completo y siempre actual — 
reglas fundamentales, convenciones de codificación, flujo de revisión — está en 
**[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Tu primera contribución en ~10 minutos

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 warnings, or CI will politely refuse you
dotnet test           # unit + integration + E2E
```

¿Encontraste algo para arreglar? Ramifícalo, cámbialo, añade una prueba y abre un PR. Ese es el bucle completo.

## Formas de ayudar (no todas son código)

| Contribución | Esfuerzo | Dónde |
|---|---|---|
| 🐛 Reportar un error reproducible | 10 min | [Informe de errores](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Sugerir una característica | 10 min | [Solicitud de características](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Mejorar estos documentos | 15 min | Editar bajo `website/docs/` y PR |
| 🧪 Añadir una prueba faltante | 30 min | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Reportar el comportamiento exacto de cTrader | 10 min | [Abrir una discusión](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Las reglas de la casa (versión corta)

cMind mueve **dinero real**, por lo que algunas cosas son innegociables — 
y honestamente, hacen que la base de código sea una alegría para trabajar:

- **Diseño guiado por el dominio estricto.** La lógica de negocio vive en agregados y objetos de valor, 
  nunca en puntos finales o interfaz de usuario. (Hay un libro de jugadas amigable para ello en el repositorio.)
- **Tres niveles de prueba, cada cambio.** Unidad + integración + E2E, 
  *incluyendo* rutas de fallo (conexiones caídas, órdenes rechazadas, nodos muertos). 
  Las pruebas verdes son el precio de la admisión.
- **Cero advertencias.** `TreatWarningsAsErrors=true`. Idiomas modernos de C# 14.
- **Sin secretos, sin cadenas mágicas, nunca `DateTime.UtcNow`** (inyecta `TimeProvider` en su lugar).
- **Documentos en el mismo commit.** Cambia el comportamiento → actualiza su documentación. Sí, eso incluye este sitio.

Detalle completo, con el *por qué* detrás de cada regla, en
[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) y
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Contribuyendo con IA 🤖

Genuinamente bienvenidas **PR asistidas por IA** — este proyecto está construido para ser trabajado por agentes 
así como por humanos. Si estás conduciendo Claude, Copilot o similar: apúntalo a
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), déjalo leer los archivos 
`CLAUDE.md` anidados, y mantenlo al mismo nivel (pruebas, cero advertencias, DDD). 
Un buen PR de IA es indistinguible de un buen PR humano — la misma revisión, la misma bienvenida.

## Sé excelente con los demás

Tenemos un [Código de conducta](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md).
El resumen: sé amable, asume buena fe y recuerda que hay una persona 
(o el agente de una persona) en el otro extremo. Haz preguntas temprano — eso es una fortaleza, no una molestia.

Bienvenido a bordo. No podemos esperar a ver qué construyes. 🎉
