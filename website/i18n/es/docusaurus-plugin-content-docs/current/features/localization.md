---
slug: localization
sidebar_label: Localization
---

# Localización (i18n)

cMind es totalmente localizable y se envía en los **mismos 23 idiomas que cTrader mismo soporta**, así que un trader usa la plataforma — y lee estos docs — en su propio idioma. El inglés es el fallback; cualquier traducción faltante se degrada elegantemente a inglés en lugar de mostrar un espacio en blanco o una clave sin traducir.

## Idiomas soportados

Árabe (RTL), Chino (Simplificado), Checo, Inglés, Francés, Alemán, Griego, Húngaro, Indonesio, Italiano, Japonés, Coreano, Malayo, Polaco, Portugués (Brasil), Ruso, Serbio, Eslovaco, Esloveno, Español, Tailandés, Turco, Vietnamita.

La única fuente de verdad es `Core.Constants.SupportedCultures` — el middleware de cultura de request, el conmutador de idioma, la prueba de paridad de recursos y la compuerta de sin texto hardcoded todas leen de ella. Añadir un idioma es un cambio de una línea allí más sus archivos de recursos.

## Cómo funciona (Blazor Server)

- **Recursos.** Los textos de UI viven en `src/Web/Resources/Ui.resx` (base en inglés) más un `Ui.<cultura>.resx` por idioma. Los componentes los leen a través de `IStringLocalizer<Ui>` — `@L["clave"]`, nunca un literal. Los archivos `.resx` se generan desde `tools/i18n/ui-translations.json` (`pwsh tools/i18n/gen-resx.ps1`), la fuente de verdad amigable para traductores.
- **Resolución de cultura.** `RequestLocalizationMiddleware` elige la cultura primero de la cookie `.AspNetCore.Culture`, luego de `Accept-Language` del navegador, luego inglés.
- **Cambio.** El conmutador de idioma en la barra de app (y la sección **Settings → Language**) navega al endpoint `GET /set-culture` — una recarga completa fuera del circuito Blazor, porque un circuito no puede cambiar la cultura en vivo. Escribe la cookie y, para un usuario conectado, persiste la elección en su perfil (`UserProfile.Locale`); la recarga inicia un nuevo circuito en el idioma elegido.
- **Persistencia e inicio de sesión.** La configuración regional guardada del perfil se escribe de nuevo en la cookie de cultura al iniciar sesión, así que un usuario aterriza en su idioma en cada dispositivo.
- **Derecha a izquierda.** El árabe (y cualquier idioma RTL futuro) establece `<html dir="rtl">` y envuelve el layout en `MudRTLProvider` de MudBlazor, reflejando todo el shell.
- **ICU.** El host web se ejecuta con ICU habilitado (`InvariantGlobalization=false`); el código de wire/parse se mantiene en `CultureInfo.InvariantCulture`, así que solo el formato de UI por cultura se ve afectado — nunca un backtest o CSV.

## La compuerta — sin texto UI hardcoded

Las nuevas cadenas user-facing **no pueden** fusionarse sin localizar en el alcance cubierto:

- Una prueba de guardia de arquitectura que falla en build (`NoHardcodedUiTextTests`) escanea los archivos `.razor` migrados y falla en cualquier literal, atributo que lleva texto (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`, `aria-label`, `alt`) que no sea una búsqueda `@L["…"]`.
- Una prueba de paridad de recursos (`ResourceParityTests`) falla el build si a algún idioma le falta una clave o envía un valor en blanco — cada idioma siempre tiene cada clave.

## Añadir o cambiar una cadena

1. Añadir/editar la clave en `tools/i18n/ui-translations.json` para **cada** cultura.
2. Regenerar los `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Referenciarla en el componente con `@L["tu.clave"]`.
4. `dotnet test` — las pruebas de paridad y texto hardcoded te mantienen honesto.

## Localización de docs

Estos docs también están localizados. El i18n de Docusaurus está configurado para los 23 locales (`website/i18n/`), con un dropdown de locale en la navbar y RTL para árabe. Genera el andamiaje de archivos de traducción de un locale con `npm run write-translations -- --locale <code>` y traduce bajo `website/i18n/<code>/`. Según el mandato de localización, **añadir o cambiar cualquier doc significa actualizar cada locale en el mismo cambio.**