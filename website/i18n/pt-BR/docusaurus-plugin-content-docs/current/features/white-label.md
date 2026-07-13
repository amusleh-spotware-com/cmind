---
description: "Revendedor marca novamente o aplicativo — nome do produto, logotipo, favicon, cores, CSS personalizado — via configuração de implantação, nenhuma mudança de código. Cada valor de marca padrão é para estoque…"
---

# Marca branca

Revendedor marca novamente o aplicativo — nome do produto, logotipo, favicon, cores, CSS personalizado — via configuração de implantação, nenhuma mudança de código. Cada valor de marca **padrão para identidade de estoque**: implantação não configurada parece igual a antes; revendedor substitui apenas o que precisa.

## Modelo

- `Core.Options.BrandingOptions` — vinculado a partir de `App:Branding`. Baseado em string (borda de configuração); cada cor validada quando tema construído.
- `Core.Branding.HexColor` — objeto de valor para cor hexadecimal CSS (`#RGB` / `#RRGGBB`), imutável, auto-validação.
  Cor inválida lança `DomainException` (`domain.branding.color_invalid`) quando tema construído — implantação mal configurada falha rápido na inicialização, não renderiza paleta quebrada.
- `Web.Components.Theme.Build(BrandingOptions)` — produza tema MudBlazor de marca. Apenas entradas de paleta marcadas vêm de configuração; tipografia, layout, tons de superfície neutra permanece fixos para que produto mantenha aparência coerente entre revendedores.
- `Web.Branding.IBrandingThemeProvider` — singleton, construir tema uma vez, reconstruir na mudança de opções.
  Injetado por `MainLayout`/`EmptyLayout` para `MudThemeProvider`, pela barra de aplicativos para nome/logotipo do produto. `App.razor` leia `IOptionsMonitor<AppOptions>` diretamente para página `<head>` (título, descrição, favicon, tema-cor, CSS personalizado).

## Configuração

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — copy trading and strategy automation.",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "AppBarColor": "#0B1220",
      "BackgroundColor": "#0E1525",
      "SurfaceColor": "#161E30",
      "SuccessColor": "#3FB950",
      "ErrorColor": "#F85149",
      "WarningColor": "#D29922",
      "InfoColor": "#2D7FF9",
      "CustomCss": ".mud-appbar { letter-spacing: 1px; }"
    }
  }
}
```

Forma variável de ambiente: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Chave | Efeito | Padrão |
|-----|--------|---------|
| `ProductName` | Texto da barra de aplicativos + página `<title>` | `cMind` |
| `LogoUrl` | Logotipo da barra de aplicativos; quando vazio, texto do nome do produto mostra | *(vazio)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | descrição de estoque |
| `PrimaryColor` / `SecondaryColor` | acento, ícone de gaveta, botões | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + superfícies; `AppBarColor` conduz `<meta theme-color>` + manifesto PWA `theme_color`, `BackgroundColor` a `background_color` do manifesto | paleta escura |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | cores de status | estoque |
| `CustomCss` | `<style>` injetado em `<head>` (implantação confiável) | *(vazio)* |
| `ShowSiteLink` | mostrar o link de crédito "Powered by cMind" no painel | `true` |
| `RequireMfa` | exigir que cada usuário configure autenticação de dois fatores antes de usar o aplicativo | `false` |
| `NodesUi` | quanto da superfície Nodes é enviado: `Full` (lista + adicionar/excluir manual), `Monitor` (lista somente leitura, sem adicionar/excluir), `Hidden` (sem navegação, sem página, sem API manual) | `Full` |
| `RestrictNodesToOwner` | quando `true`, apenas o proprietário pode ver/gerenciar nós; caso contrário, toda a superfície de pessoal administrador ou acima pode. Usuários normais nunca vêem nós de qualquer forma | `false` |

Ativos referenciados por `LogoUrl`/`FaviconUrl` servidos do aplicativo web `wwwroot` (ex. monte pasta `wwwroot/branding/`) ou qualquer URL absoluta.

`App:Branding` validado na inicialização (`BrandingOptionsValidator`, executado via `ValidateOnStart`): toda cor deve ser hex válida, `CustomCss` não deve conter `<`/`>` (não pode quebrar a tag `<style>`). Implantação mal configurada falha na inicialização com mensagem clara, não renderiza página quebrada.

## Link Powered-by

O painel renderiza um pequeno link de crédito **"Powered by cMind"** que aponta para o site de documentação do projeto. É controlado por `App:Branding:ShowSiteLink` e é **`true` por padrão** — uma implantação não configurada o mostra. Um revendedor que executa uma instância totalmente marcada em branco define `App__Branding__ShowSiteLink=false` para removê-lo inteiramente.

O link é emitido pelo componente do painel e lê o sinalizador através de `IBrandingThemeProvider` / `BrandingOptions`, portanto ativar/desativar é uma mudança apenas de configuração (sem reconstrução). Veja [White-label para negócios](../white-label-for-business.md#the-powered-by-cmind-link) para o resumo voltado para negócios.

## Lista de permissões de corretores

Uma implantação com marca branca pode restringir quais contas de corretores de negociação seus usuários podem adicionar — para que um corretor que executa cMind apenas para seus próprios clientes apenas serve seu próprio livro. Configurado em `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Forma variável de ambiente: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Comportamento:**

- **Lista vazia (padrão) ⇒ irrestrita.** Cada corretor é permitido e **nenhuma verificação é executada** — uma implantação de estoque é completamente inalterada.
- **Não vazio ⇒ restrito.** cMind verifica cada conta que um usuário tenta adicionar contra a lista (insensível a maiúsculas/minúsculas):
  - **Link Open API (OAuth)** — o nome do corretor é relatado com autoridade pela cTrader Open API, portanto uma conta não permitida é simplesmente **ignorada** (contas permitidas na mesma concessão ainda link); a página de autorização diz ao usuário quais corretores foram ignorados.
  - **cID manual (nome de usuário / senha)** — o corretor digitado pelo usuário **não** é confiável. cMind **verifica** o corretor real da conta executando o cBot de sondagem do corretor enviado através da CLI cTrader (lendo `Account.BrokerName`) e persiste esse nome verificado. Um corretor não permitido é rejeitado com uma notificação; uma falha de verificação (credenciais ruins, sem nó, timeout) também é exibida e a conta não é adicionada.

**Modelo:**

- `Core.Options.AccountsOptions` — vinculado a partir de `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`, `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — objeto de valor (cortado, igualdade insensível a maiúsculas/minúsculas).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; vazio = permitir tudo. Aplicado como invariante dentro de `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount` (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — executa o contêiner de sondagem no host da web (que tem o soquete Docker), rastreia logs e analisa o corretor via `Core.Accounts.BrokerProbeOutput`. Invocado apenas quando a lista de permissões é restrita.

**cBot de sondagem de corretor:** um `broker-probe.algo` pré-construído é enviado com o aplicativo web (`src/Web/BrokerProbe/`, copiado para a saída como `broker-probe/broker-probe.algo`), para que o padrão `App:Accounts:BrokerProbeAlgoPath` se resolva fora da caixa — um caminho relativo é resolvido contra o diretório base do aplicativo, um caminho absoluto é usado como fornecido. A fonte vive em `tools/broker-probe/`. Quando o algo está ausente, a verificação manual de cID falha fechada — contas em uma lista de permissões restrita ainda podem ser vinculadas via o caminho Open API, que não precisa de sondagem.

## Lista de permissões de corretores — testes

- **Unidade** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` objetos de valor, analisador `BrokerProbeOutput` e invariante de lista de permissões `CTraderIdAccount`.
- **Integração** — `IntegrationTests/BrokerAllowlistTests.cs`: endpoint manual-cID com verificador falso (irrestrito / verificado / não permitido / falha de verificação) + vinculador Open API ignorando contas não permitidas. `BrokerVerifierLiveTests.cs` executa a **sondagem real** quando credenciais cID + algo são fornecidos (ignora limpamente caso contrário).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: uma implantação restrita rejeita um adicionar manual através da interface real e mostra a notificação "não conseguiu verificar" (sem linha de conta adicionada).

## Visibilidade da interface de nós

Nodes é infraestrutura que a maioria dos inquilinos nunca gerencia manualmente — agentes cTrader CLI [auto-registram e pulsam](../operations/node-discovery.md), para que uma implantação com marca branca possa ocultar os controles manuais ou a superfície Nodes inteira e ainda execute um cluster saudável através de auto-descoberta.
Duas chaves de marca apenas de configuração governam isso:

```json
{
  "App": {
    "Branding": {
      "NodesUi": "Monitor",
      "RestrictNodesToOwner": true
    }
  }
}
```

Forma variável de ambiente: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — três modos:**

- **`Full` (padrão)** — o produto de estoque: a lista de nós mais os controles manuais **Novo Node** e **Excluir**. `POST`/`DELETE /api/nodes` trabalho.
- **`Monitor`** — uma superfície somente leitura: a lista e estatísticas ao vivo permanecem, mas adicionar e excluir manuais são removidos. Os nós aparecem apenas através da auto-descoberta. `POST`/`DELETE /api/nodes` retornam **404**.
- **`Hidden`** — o link de navegação Nodes e página desaparecem inteiramente e a rota de página redireciona para o painel; a API manual de adicionar/excluir está desativada. O cluster é apenas auto-descoberta.

**`RestrictNodesToOwner`** pisos que podem ver e gerenciar nós. O padrão `false` mantém a superfície de pessoal **admin-ou-acima** padrão (`AdminOrAbove`); defina como `true` para torná-lo **apenas proprietário** (`Owner`). De qualquer forma, **usuários normais nunca veem nós** — isso apenas escolhe entre apenas proprietário e a superfície de pessoal mais ampla.

A **auto-descoberta de nó é afetada por ambas as chaves**: o ponto final de auto-registro + pulso anônimo `POST /api/nodes/register` sempre funciona, para que uma implantação `Hidden`/`Monitor` ainda cresça seu cluster automaticamente.

**Modelo:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — a única fonte de verdade compondo o modo + restrição de proprietário: `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav (`NavMenu.razor`), a página (`Pages/Nodes.razor`) e os endpoints (`NodeEndpoints`) tudo lê para que a interface e API nunca possam discordar.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — vinculado a partir de `App:Branding`.

## Testes de visibilidade da interface de nós

- **Unidade** — `UnitTests/Nodes/NodesUiAccessTests.cs`: resolução de visibilidade de página, gerenciamento manual e política necessária em cada modo + marca padrão.
- **Integração** — `IntegrationTests/NodeUiGatingTests.cs`: sobre HTTP real + Postgres — `Full` permite um adicionar manual, `Monitor`/`Hidden` 404 adicionar e excluir, e `RestrictNodesToOwner` proíbe um administrador enquanto o proprietário ainda lê a lista.
- **E2E** — `E2ETests/NodesUiTests.cs` (padrão `Full`: link de navegação + página + botão Novo Node renderizam) e `E2ETests/NodesHiddenTests.cs` (`Hidden`: link de navegação desaparecido, `/nodes` redireciona).

## Tokens de design (variáveis CSS)

Marca também alcança a **própria** folha de estilo do aplicativo + componentes personalizados, não apenas MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emite a paleta marcada como propriedades personalizadas CSS em `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), injetado em `App.razor` logo após `site.css`. `site.css` e cada componente leem `var(--app-*)` — **sem cores codificadas** — para que a paleta de revendedor flua em todos os lugares (herói de login, nav inferior, dicas de ajuda, página offline) gratuitamente. Tons de superfície neutra padrão em `site.css :root`; `CustomCss` (injetado por último) pode substituir qualquer token. Veja [ui-guidelines.md](../ui-guidelines.md) §2.

## PWA marcada

O aplicativo instalável também é marcado — o ponto final do manifesto (`/manifest.webmanifest`) é construído a partir de `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → tema/background). Veja [pwa.md](pwa.md).

## Testes

- **Unidade** — `UnitTests/Branding/HexColorTests.cs`: validação de hex válido/inválido.
- **Integração** — `IntegrationTests/ThemeBuildTests.cs`: cores são mapeadas na paleta, cor inválida lança; `IntegrationTests/BrandingHttpTests.cs`: `ProductName` personalizado/descrição/cor de tema renderizam em página `<head>` servida (WebApplicationFactory + Postgres), os padrões mantêm nome de estoque.
- **E2E** — `E2ETests/BrandingTests.cs`: nome de produto marcado renderiza em barra de aplicativos em navegador real.
