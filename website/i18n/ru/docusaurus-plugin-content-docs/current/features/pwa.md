---
description: "cMind устанавливается на телефон или desktop как native приложение — home-screen значок, standalone окно, splash и friendly offline страница. Это mobile-first и…"
---

# Installable приложение (PWA)

cMind устанавливается на телефон или desktop как native приложение — home-screen значок, standalone окно, splash и friendly offline страница. Это **mobile-first** и полностью responsive; смотрите [ui-guidelines.md](../ui-guidelines.md).

## Что "installable" означает здесь — и честный лимит

Blazor **Server** рендерит через live SignalR circuit, поэтому приложение не может запустить полностью offline. Что PWA доставляет:

- **Installable** — valid web manifest + значки, поэтому браузеры предлагают *Install* / *Add to Home Screen*.
- **App-shell кешировано** — service worker кеширует static assets (CSS, значки, manifest) и показывает **offline страница** когда сеть падает, вместо browser ошибки.
- **Native feel** — standalone display, брендированные theme-color/status bar, app значок, iOS home-screen значок.

Это **не** обеспечивает offline interactivity — это требовало бы Blazor WebAssembly (отдельный будущий track). Не обещайте offline использование live функций.

## Куски

| Кус | Где |
|-------|-------|
| Manifest (динамический, брендированный) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anonymous) |
| Значки (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Service worker (app-shell) | `Web/wwwroot/service-worker.js` |
| Offline fallback страница | `Web/wwwroot/offline.html` |
| Registration + iOS tags + install-prompt capture | `Web/Components/App.razor` |
| Route константы | `Core.Constants.PwaRoutes` |

### Manifest

Сервирован динамически из `BrandingOptions` поэтому reseller's product имя, цвета и значки carry в installed приложение: `name`/`short_name` из `ProductName`, `description`, `theme_color` из `AppBarColor`, `background_color` из `BackgroundColor`, `display: standalone` и icon set (incl. **maskable** 512 для clean Android значка). Anonymous — install prompt должен работать перед sign-in.

### Service worker

App-shell только. Это **никогда** не intercepts Blazor circuit (`/_blazor`), framework (`/_framework`), или SignalR hubs (`/hubs`) — те всегда сеть. Navigations network-first с offline страницей как fallback; static assets (`/css`, `/icons`, `/_content`) cache-first с background revalidate. Зарегистрирован с `updateViaCache: 'none'` поэтому worker обновления apply надежно. Caches версионированы (`cmind-shell-v<n>`) — bump на shell изменения.

### iOS

iOS игнорирует manifest значки/splash, поэтому `App.razor` также эмиттирует `apple-touch-icon` и `apple-mobile-web-app-*` meta tags. iOS имеет нет `beforeinstallprompt`; пользователи устанавливают через Safari's *Add to Home Screen*. `beforeinstallprompt` захвачен в `window.deferredInstallPrompt` на Chromium/Android для custom install affordance.

## Тесты

- **E2E** — `E2ETests/PwaTests.cs`: manifest сервирован с `application/manifest+json`, non-empty значки incl. maskable один, `display: standalone`, `apple-touch-icon` linked и service worker registers + activates. `MobileLayoutTests` / `MobileDialogTests` покрывают mobile shell PWA устанавливает.
