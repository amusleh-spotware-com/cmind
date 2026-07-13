---
description: "Obrigatório para cada novo ou alterado pedaço de interface neste aplicativo (páginas Blazor, diálogos, componentes). Esta é a fonte de verdade referenciada por CLAUDE.md. Se uma…"
---

# Diretrizes de Design de Interface - OBRIGATÓRIO

Obrigatório para **cada** novo ou alterado pedaço de interface neste aplicativo (páginas Blazor, diálogos, componentes). Esta é a fonte de verdade referenciada por `CLAUDE.md`. Se uma regra o bloqueia, pare e pergunte — não envie interface que viole. Enraizado em `plans/ui-overhaul.md`.

## 1. Mobile-first, sempre

- **Autor para um telefone 360–430px primeiro**, depois melhore para cima com consultas de mídia `min-width` / props de ponto de interrupção MudBlazor. Nunca desktop-first com substituições `max-width`.
- **Sem scroll horizontal em qualquer largura 320–1920px.** Se o conteúdo é mais largo que a viewport, é um bug.
- Alvo de toque ≥ **44px** (`var(--app-touch-target)`). Entradas de texto ≥ 16px fonte (impede zoom de foco do iOS).
- Respeite entalhos: use `env(safe-area-inset-*)`; a viewport já define `viewport-fit=cover`.
- Honre `prefers-reduced-motion` — nenhuma informação essencial transmitida apenas por animação.

## 2. Tokens de design — sem valores hard-coded

- Todas as cores/raio/espaçamento vêm de **tokens de design**: tema MudBlazor (`Web/Components/Theme.cs`) + as propriedades personalizadas CSS emitidas por `Web/Branding/BrandingCss.cs` (`var(--app-primary)`, `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nunca hard-code uma cor hex, raio ou cadeia de marca em um componente ou regra CSS.** Leia um token. Os tokens fluem de `BrandingOptions` com marca branca, para que a paleta de um revendedor deva alcançar sua interface gratuitamente.
- Novo valor afetando marca → adicione um token + campo de marca; não inline.

## 3. Layout responsivo e dados

- **Tabelas entram em colapso para cartões em telefones.** Cada `MudTable` define `Breakpoint="Breakpoint.Sm"` e cada `MudTd` tem um `DataLabel`. Nenhuma tabela larga bruta no celular. (Modelo: `Components/Pages/Nodes.razor`.)
- Grades: `MudItem xs="12" sm="6" md="4"` — largura total no telefone, multi-coluna para cima.
- Formulários em coluna única no celular; grandes alvos de toque; `inputmode`/`autocomplete` em entradas; inputmode numérico/decimal para dinheiro/por cento.
- Forneça **carregamento, vazio e erro** estados em cada lista/detalhe — dimensionado para celular.
- A **navegação inferior** do celular (`Components/Layout/BottomNav.razor`) é a navegação telefônica principal; a gaveta agrupada é o menu completo. Adicione destinos de tráfego alto; mantenha ≤5 itens.

## 4. Diálogos (criar/editar)

- Todas as ações adicionar/criar/editar/novo usam um **diálogo MudBlazor** (`IDialogService.ShowAsync<TDialog>`), nunca um formulário de página inline. Os diálogos vivem em `Web/Components/Dialogs/`, exponham `[Parameter]`s, retornem um `public sealed record …Result(...)` aninhado. As ações de linha de lista (iniciar/parar/excluir) permanecem inline como botões de ícone.
- Nos telefones, os diálogos devem ser **tela inteira / largura total** e conscientes de teclado.

## 5. Ajuda inline — cada controle

- Cada opção, selecionar, alternar ou ação não óbvia recebe uma **`<HelpTip Text="…" />`** (`Components/HelpTip.razor`) — pairar no desktop, **tocar no celular**. Fonte o texto de `docs/` para que a orientação fique em sincronização com o comportamento; atualize ambos no mesmo commit.

## 6. Marca branca

- Nome do produto, logo, descrição, suporte/empresa, cores, favicon tudo vem de `BrandingOptions`. Faça referência a eles (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nunca "cMind" literal ou uma cor de marca. O manifesto PWA, ícones, cor de tema e herói de login são todos marcados.

## 7. PWA

- O aplicativo é instalável. Mantenha o ponto final do manifesto (`/manifest.webmanifest`) marcado, ícones presentes (192/512/maskable + apple-touch), o trabalhador de serviço apenas app-shell (nunca tocando no circuito Blazor/`_framework`/hubs) e a página offline funcionando. Nova rota estática → mantenha escopo manifesto.
- Blazor Server precisa de um circuito SignalR ao vivo → **instalável + app-shell**, não totalmente offline. Não prometa interatividade offline.

## 8. Acessibilidade

- Rótulos em entradas, `aria-*` em controles personalizados, foco visível, ordem de foco lógica. Como o tema é personalizável com marca branca, verifique **contraste** contra o tema ativo, não uma paleta fixa.

## 9. E2E — nenhuma interface envia sem teste (bloqueio)

Cada mudança voltada para o usuário envia Playwright E2E em `tests/E2ETests`, dirigida como um usuário real, **em emulação de dispositivo móvel** além do desktop:

- Nova rota → adicione ao `PageSmokeTests` **e** `MobileLayoutTests` (renderiza, nav inferior, nenhuma interface de erro).
- Converta uma tabela/página → adicione sua rota ao conjunto móvel **sem overflow**.
- Novo fluxo → uma jornada móvel realista (rodada de criar/editar/salvar) **e** um caminho infeliz (entrada inválida, lista vazia, permissão negada por papel).
- Nova dica de ajuda → afirme que abre ao tocar (padrão `HelpTipTests`).
- Use `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulação de dispositivo).
- `dotnet test` verde antes de "pronto". WebKit emulado ≠ Safari móvel — gating de dispositivo real é uma etapa de lançamento separada.

## 10. Definição de concluído (Interface)

- [ ] Mobile-first; sem overflow horizontal 320–1920px; alvos de toque ≥44px.
- [ ] Apenas tokens de design — zero cores/raios/cadeias de marca hard-coded.
- [ ] Tabelas → cartões no telefone (`DataLabel` + `Breakpoint.Sm`); estados de carregamento/vazio/erro presentes.
- [ ] Criar/editar via diálogo; tela inteira no celular.
- [ ] Cada controle tem um `HelpTip` originário de documentos.
- [ ] Marca branca + PWA respeitados.
- [ ] E2E móvel + desktop adicionado (fumaça, sem overflow, jornada, caminho infeliz); `dotnet test` verde.
- [ ] Cavaleiro `get_file_problems` + `dotnet format analyzers` limpo em arquivos tocados.
