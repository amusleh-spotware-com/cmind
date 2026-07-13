---
title: Dashboard
description: O painel cMind — um centro de comando ao vivo, mobile-first para suas execuções de cBot, backtests, recursos e cluster de nós.
---

# Dashboard 📊

A primeira coisa que você vê quando entra, e honestamente a página que você deixará aberta o dia todo. A
página inicial (`/`, `Components/Pages/Index.razor`) é um **centro de comando ao vivo, mobile-first** para a atividade do usuário conectado
através de execuções de cBot, backtests, recursos e (para admins) o cluster de nó. Ele se atualiza automaticamente, parece ótimo em um telefone e nunca faz você pressionar F5.

## O que ele mostra

De cima para baixo, ordenado por prioridade para um telefone (cada bloco é um item de pilha de largura completa em móvel, uma
grade responsiva em tablet/desktop):

1. **Cabeçalho** — título, um indicador ao vivo (um ponto pulsante real; estático sob `prefers-reduced-motion`), o
   tempo da última atualização e um **alternância de período** (`1H · 24H · 7D · 30D`) que orienta os KPIs e gráfico.
2. **KPIs de herói** — quatro cartões relançáveis, cada um um grande número + um sparkline SVG inline, e (onde
   significativo) um **delta vs o período anterior**:
   - **Ativo agora** — execuções + backtests começando/rodando atualmente.
   - **Taxa de sucesso** — completado ÷ (completado + falhou) no período; delta em pontos percentuais.
   - **Completado** — execuções/backtests terminados este período; delta vs período anterior.
   - **Falhou** — falhas este período; delta (menos é melhor, então uma queda mostra verde).
3. **Gráfico de atividade** — um cronograma de área ApexCharts de iniciado / completado / falhou por intervalo de tempo.
4. **Anel de status de instância** — uma rosca de em execução / backtests / pendente / completado / falhou, total no
   centro.
5. **Backtests** — um snapshot de três tiles (em execução / completado / falhou), click-through para `/backtest`.
6. **Copy trading** — seus perfis de copy-trading com um ponto de status ao vivo, contagem de destinos e um crachá **Live**
   em perfis em execução; click-through para `/copy-trading`.
7. **Agentes de IA** — seus agentes de negociação dirigidos por persona com estado de execução (arquétipo · status) e tempo da última ação;
   click-through para `/agent-studio`.
8. **Feed de atividade ao vivo** — os 20 eventos mais recentes (mais novos primeiro) com um ponto colorido por status e um
   carimbo de data/hora relativo.
9. **Saúde do cluster** (apenas admins) — nós ativos vs totais e um medidor de capacidade em uso.
10. **Tiles de recursos** — cBots, contas de negociação, IDs cTrader, chaves MCP (clique através de suas páginas).

## Personalize seu painel

Cada bloco acima é um **widget que você controla**. Pressione **Personalizar** (parte superior direita do cabeçalho) para abrir um
diálogo onde você **mostrar/ocultar** qualquer widget e **reordenar** eles com setas para cima/baixo. **Redefinir para padrão**
restaura a ordem de catálogo. Sua escolha é **persistida no servidor por usuário**, então segue você entre
navegadores e dispositivos — não apenas esta guia.

- Widgets com portão de características e apenas admin (Copy trading, agentes de IA, Saúde do cluster) apenas aparecem no
  diálogo quando sua implantação/função pode usá-los.
- O catálogo de widgets é uma única fonte de verdade em `Core/Dashboard/DashboardWidgets.cs`; apresentação
  (rótulo + ícone + disponibilidade) vive em `Components/Dashboard/DashboardWidgetMeta.cs`.

## Como ele fica ao vivo

A página consulta `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` a cada 10 segundos e re-renderiza o
widgets no local — sem recarga manual. Uma falha de busca transitória é engolida e retentada no próximo tick;
o loop para limpo em dispose. A primeira carga mostra um esqueleto; uma falha persistente mostra um cartão de erro com **Retry**; um usuário sem dados vê KPIs zerados e cópia de estado vazio.

## Backend

- `Endpoints/DashboardEndpoints.cs` mapeia `/overview` (e mantém o escalar `/stats` mais antigo). É
  por usuário e portão admin via `ICurrentUser`; o relógio vem de `TimeProvider`. Também mapeia
  `GET/PUT /api/dashboard/layout` — layout de widget do usuário, carregado na inicialização de página e salvo do
  diálogo Personalizar.
- **Persistência de layout** é o agregado `UserDashboard` (`Core/Dashboard/UserDashboard.cs`): uma placa
  por usuário (único em `UserId`), possuindo uma lista ordenada de configurações de widget (visível + ordem) armazenadas como um
  coluna `jsonb`. A lista ordenada é apenas mutada através de `Apply` / `Reset`, que validam cada
  chave contra o catálogo `DashboardWidgets` e mantêm a coleção completa e desduplicada. Chaves desconhecidas
  são rejeitadas com um `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` constrói o modelo de leitura composto `DashboardOverview`: um
  snapshot de status de todos os tempos (contagens agrupadas), um conjunto com janelas de instâncias materializadas uma vez e contagens de recursos/nó.
  Status de instância e carimbos de data/hora terminal vivem em subtipos TPH (não colunas), então linhas são lidas na memória
  via `InstanceEndpoints.GetStartedAt/GetStoppedAt` helpers compartilhados. Hora do evento =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` mantém os DTOs, o período→(janela, plano de contagem de intervalo) e
  `DashboardMath` — pura, matemática determinística de binning + KPI/delta (sem I/O, `now` é passado).

Os deltas de KPI comparam a janela atual contra a imediatamente anterior (a consulta busca uma janela dupla para isto). Não há **feed de P&L de conta ao vivo** — a plataforma tem apenas patrimônio para backtests e
rastreamento prop-firm — então o painel é deliberadamente *operacional* (atividade, taxa de transferência, taxa de sucesso),
não um ticker de saldo de corretagem.

## Design & tokens

Toda cor vem de tokens de design (`var(--app-success|-warning|-error|-info|-primary|-text*)`), então uma
paleta de rótulo branco flui de graça — incluindo o gráfico, cujas cores de série são lidas de
tokens resolvidos em tempo de execução via `window.appReadTokens` (SVG não pode consumir variáveis CSS diretamente). Nenhum
hex codificado em lugar algum no painel. Veja [../ui-guidelines.md](../ui-guidelines.md).

## O link "Powered by cMind"

O painel mostra um pequeno e elegante **link "Powered by cMind"** que aponta para este site de documentação.
É **mostrado por padrão** — somos orgulhosos do projeto e ajuda outros traders a encontrá-lo — mas é completamente sua decisão.
Revendedores executando uma instância totalmente com marca branca alternam `App:Branding:ShowSiteLink` para `false` e desaparece. Veja
[Marca de rótulo branco](./white-label.md#powered-by-link).

## Testes

- **Estilo-unitário** (`tests/IntegrationTests/DashboardMathTests.cs`) — binning, taxa de sucesso,
  deltas de período anterior, parse de período, vazio/limite (evento em `now`, proteção de divisão por zero).
- **Unidade** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — agregado `UserDashboard`: semeadura padrão,
  aplicar ordem/visibilidade, anexar omitido, colapso duplicado, rejeição de chave desconhecida, resetar.
- **Integração** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — leitura
  modelo contra Postgres real (status/KPIs/atividade/recursos, saúde de nó admin, caminho de usuário vazio) e as novas seções backtests/perfis de cópia/agentes e uma **rodada de layout** (salvar layout personalizado → recarregar →
  ordem + visibilidade persistidas).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — desktop + móvel: cartões de KPI,
  gráfico, anel e feed renderizam; o alternância de período muda o período ativo e recarrega; um KPI
  faz drill-through para `/run`; **ocultar um widget persiste entre recarregamentos**, **Reset** o traz de volta e
  o diálogo Personalizar funciona em um telefone sem overflow horizontal. `/` também está em `PageSmokeTests`,
  `MobileLayoutTests` (shell + nenhum-overflow) e `MobileJourneyTests`.
