---
description: "Vinculação obrigatória para cada nova ou alterada peça da UI neste aplicativo (páginas Blazor, diálogos, componentes). Esta é a fonte de verdade referenciada por CLAUDE.md. Se uma…"
---

# UI Design Guidelines — MANDATORY

Vinculação obrigatória para **toda** nova ou alterada peça da UI neste aplicativo (páginas Blazor, diálogos, componentes).
Esta é a fonte de verdade referenciada por `CLAUDE.md`. Se uma regra bloqueá-lo, pare e pergunte — não
envie UI que a viole. Enraizada em `plans/ui-overhaul.md`.

## 1. Mobile-first, always

- **Projete para um telefone de 360–430px primeiro**, depois melhore para cima com `min-width` media queries / props de breakpoint do MudBlazor. Nunca comece no desktop com `max-width` overrides.
- **Sem scroll horizontal em nenhuma largura 320–1920px.** Se o conteúdo for mais largo que a viewport, é um bug.
- Touch targets ≥ **44px** (`var(--app-touch-target)`). Entradas de texto ≥ 16px de fonte (impede zoom-on-focus do iOS).
- Respeite notches: use `env(safe-area-inset-*)`; a viewport já define `viewport-fit=cover`.
- Honre `prefers-reduced-motion` — nenhuma informação essencial transmitida apenas por animação.

## 2. Design tokens — no hard-coded values

- Todas as cores/raios/espaçamentos vêm de **design tokens**: tema do MudBlazor (`Web/Components/Theme.cs`) +
  as propriedades CSS customizadas emitidas por `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nunca hard-code uma cor hex, raio, ou string de marca em um componente ou regra CSS.** Leia um token.
  Tokens fluem de `BrandingOptions` de white-label, então a paleta de um revendedor deve alcançar sua UI gratuitamente.
- Novo valor que afeta a marca → adicione um token + campo de branding; não o inline.

## 3. Responsive layout & data

- **Tabelas colapsam para cards em telefones.** Cada `MudTable` define `Breakpoint="Breakpoint.Sm"` e cada
  `MudTd` tem um `DataLabel`. Nenhuma tabela larga bruta em celular. (Template: `Components/Pages/Nodes.razor`.)
- Grids: `MudItem xs="12" sm="6" md="4"` — largura total em telefone, multi-coluna para cima.
- Formulários em coluna única em celular; grandes alvos de toque; `inputmode`/`autocomplete` em entradas; inputmode numérico/decimal para dinheiro/percentual.
- **Controles apropriados para entrada estruturada — nunca uma caixa de texto bruta para números ou listas.** Colete números,
  dinheiro, percentuais, datas, enums e qualquer dado multi-valor com o controle correto (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, uma lista de linhas editável de campos tipados, ou uma tabela), cada campo
  validado individualmente. Um único `MudTextField` de texto livre que o usuário deve digitar um blob separado por vírgula/espaço/quebra de linha — que você então analisa — é **proibido**: é propenso a erros, não validado, e hostil
  em um telefone. **Ninguém quer digitar um blob.** Entrada multi-valor é uma lista editável de linhas tipadas (adicionar /
  remover), ou é carregada de dados de domínio existentes (por exemplo, execute a verificação direto de um backtest completo
  em vez de re-entrar seus números). `MudTextField` simples é apenas para texto genuinamente livre — nomes, notas,
  busca, descrições.
- Forneça **loading, empty, e error** estados em toda lista/detalhe — dimensionada para celular.
- A **bottom navigation** móvel (`Components/Layout/BottomNav.razor`) é a navegação primária do telefone; a
  gaveta agrupada é o menu completo. Adicione destinos de alto tráfego lá; mantenha-a ≤5 itens.

## 4. Dialogs (create/edit)

- Todas as ações adicionar/criar/editar/novo usam um **diálogo MudBlazor** (`IDialogService.ShowAsync<TDialog>`), nunca
  um formulário de página inline. Diálogos vivem em `Web/Components/Dialogs/`, expõem `[Parameter]`s, retornam um aninhado
  `public sealed record …Result(...)`. As ações de linha de lista (iniciar/parar/deletar) permanecem inline como botões de ícone.
- Em telefones, diálogos devem ser **full-screen / full-width** e keyboard-aware.

## 5. Inline help — every control

- Toda opção não óbvia, select, switch, ou ação obtém uma **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — hover no desktop, **tap no celular**. Fonte o texto de `docs/` para que
  a orientação permaneça sincronizada com o comportamento; atualize ambos no mesmo commit.

## 6. White-label

- Nome do produto, logo, descrição, suporte/empresa, cores, favicon tudo vem de `BrandingOptions`.
  Referencie-os (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nunca literal "cMind" ou uma
  cor de marca. O manifesto PWA, ícones, theme-color, e hero de login são todos branded.

## 7. PWA

- O aplicativo é instalável. Mantenha o endpoint do manifesto (`/manifest.webmanifest`) branded, ícones presentes
  (192/512/maskable + apple-touch), o service worker apenas app-shell (nunca tocando o circuito Blazor/`_framework`/hubs), e a
  página offline funcionando. Nova rota estática → mantenha o `scope` do manifesto.
- Blazor Server precisa de um circuito SignalR live → **instalável + app-shell**, não totalmente offline. Não
  prometa interatividade offline.

## 8. Accessibility

- Labels em entradas, `aria-*` em controles customizados, foco visível, ordem lógica de foco. Como o tema é
  white-labelable, verifique **contrast** contra o tema ativo, não uma paleta fixa.

## 9. E2E — no UI ships untested (blocking)

Toda mudança voltada ao usuário envia Playwright E2E em `tests/E2ETests`, conduzida como um usuário real, **em emulação de device móvel** mais desktop:

- Nova rota → adicione-a a `PageSmokeTests` **e** `MobileLayoutTests` (renderiza, bottom nav, nenhuma UI de erro).
- Converta uma tabela/página → adicione sua rota ao conjunto móvel de **no-overflow**.
- Novo fluxo → uma jornada móvel realista (rodada criar/editar/salvar) **e** um caminho infeliz
  (entrada inválida, lista vazia, permissão negada por função).
- Nova dica de ajuda → afirme que ela abre em tap (`HelpTipTests` pattern).
- Use `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulação de device).
- `dotnet test` verde antes de "done". WebKit emulado ≠ Safari móvel — gating de device real é um passo de
  release separado.

## 10. Definition of done (UI)

- [ ] Mobile-first; nenhum overflow horizontal 320–1920px; touch targets ≥44px.
- [ ] Apenas design tokens — zero cores/raios/strings de marca hard-coded.
- [ ] Tabelas → cards em telefone (`DataLabel` + `Breakpoint.Sm`); loading/empty/error estados presentes.
- [ ] Entrada estruturada usa controles validados apropriados (numérico/data/select/lista de linhas editável) — nenhuma caixa de
      texto bruta que o usuário digita um blob de número/valor delimitado.
- [ ] Criar/editar via diálogo; full-screen em celular.
- [ ] Cada controle tem uma `HelpTip` originária de docs.
- [ ] White-label + PWA respeitado.
- [ ] E2E móvel + desktop adicionado (smoke, no-overflow, journey, unhappy path); `dotnet test` verde.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` limpo em arquivos tocados.
