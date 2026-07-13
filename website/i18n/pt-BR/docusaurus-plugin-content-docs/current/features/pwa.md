---
description: "cMind instala em um telefone ou desktop como um aplicativo nativo — ícone de tela inicial, janela autônoma, splash e uma página offline amigável. É mobile-first e…"
---

# Aplicativo instalável (PWA)

cMind instala em um telefone ou desktop como um aplicativo nativo — ícone de tela inicial, janela autônoma, splash
e uma página offline amigável. É **mobile-first** e totalmente responsivo; veja
[ui-guidelines.md](../ui-guidelines.md).

## O que "instalável" significa aqui — e o limite honesto

Blazor **Server** renderiza através de um circuito SignalR ao vivo, então o aplicativo não pode executar totalmente offline. O que a
PWA entrega:

- **Instalável** — manifesto web válido + ícones, então navegadores oferecem *Instalar* / *Adicionar à Tela Inicial*.
- **App-shell em cache** — o service worker cache assets estáticos (CSS, ícones, manifesto) e mostra uma
  **página offline** quando a rede cai, em vez de um erro do navegador.
- **Sensação nativa** — exibição autônoma, cor de tema de marca/barra de status, ícone de aplicativo, ícone de tela inicial do iOS.

Ele **não** fornece interatividade offline — isso exigiria Blazor WebAssembly (uma faixa futura separada). Não prometa uso offline de características ao vivo.

## Peças

| Peça | Onde |
|-------|-------|
| Manifesto (dinâmico, marcado) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anônimo) |
| Ícones (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Service worker (app-shell) | `Web/wwwroot/service-worker.js` |
| Página de fallback offline | `Web/wwwroot/offline.html` |
| Registro + tags iOS + captura de prompt de instalação | `Web/Components/App.razor` |
| Constantes de rota | `Core.Constants.PwaRoutes` |

### Manifesto

Servido dinamicamente de `BrandingOptions` para que nome do produto, cores e ícones de um revendedor entrem no aplicativo
instalado: `name`/`short_name` de `ProductName`, `description`, `theme_color` de `AppBarColor`,
`background_color` de `BackgroundColor`, `display: standalone` e o conjunto de ícones (incluindo um **maskable**
512 para um ícone Android limpo). Anônimo — o prompt de instalação deve funcionar antes de se conectar.

### Service worker

Apenas app-shell. Ele **nunca** intercepta o circuito Blazor (`/_blazor`), framework (`/_framework`), ou
hubs SignalR (`/hubs`) — esses são sempre rede. Navegações são network-first com a página offline
como fallback; assets estáticos (`/css`, `/icons`, `/_content`) são cache-first com revalidação de fundo.
Registrado com `updateViaCache: 'none'` para que atualizações de worker se apliquem confiável. Os caches são versionados
(`cmind-shell-v<n>`) — aumente em mudanças de shell.

### iOS

iOS ignora ícones/splash de manifesto, então `App.razor` também emite `apple-touch-icon` e
`apple-mobile-web-app-*` tags meta. iOS não tem `beforeinstallprompt`; os usuários instalam através do Safari *Add to
Home Screen*. `beforeinstallprompt` é capturado em `window.deferredInstallPrompt` em Chromium/Android
para uma acessibilidade de instalação personalizada.

## Testes

- **E2E** — `E2ETests/PwaTests.cs`: manifesto servido com `application/manifest+json`, ícones não vazios incluindo
  um maskable, `display: standalone`, `apple-touch-icon` vinculado e o service worker registra +
  ativa. `MobileLayoutTests` / `MobileDialogTests` cobrem o shell móvel que a PWA instala.
