# Commitment of Traders (COT)

cMind envia um relatório **Commitment of Traders** integrado — o detalhamento semanal do CFTC de quem está
comprado e vendido no mercado futuro dos EUA (hedgers comerciais, grandes especuladores, fundos), com gráficos
históricos interativos, um **índice COT** normalizado, uma API REST autenticada para cBots e ferramentas MCP
para clientes de IA. Os dados vêm diretamente dos **conjuntos de dados públicos CFTC Socrata** — sem chave de API,
sem agregador. Como o calendário econômico, é um módulo desacoplado que pode ser desabilitado sem efeito algum
no núcleo de negociação.

## O que oferece

- **Todas as três famílias de relatórios, apenas futuros e futuros + opções combinados:**
  - **Legacy** — Non-Commercial (grandes especuladores), Commercial (hedgers), Non-Reportable.
  - **Disaggregated** — Producer/Merchant, Swap Dealers, Managed Money, Other Reportables.
  - **Traders in Financial Futures (TFF)** — Dealer, Asset Manager, Leveraged Funds, Other Reportables.
- **Um catálogo de mercado curado** — pares de forex, ouro/prata/cobre, petróleo bruto & gás natural,
  Treasuries, índices de ações, cripto e os principais grãos/commodities suaves — cada um mapeado para seu
  código de contrato CFTC estável e, onde inequívoco, para um símbolo negociável (ex: Euro FX → `EURUSD`, Ouro → `XAUUSD`).
- **O índice COT (0–100)** — onde a posição líquida atual do especulador fica dentro de seu intervalo histórico
  (lookback padrão ~3 anos). Leituras próximas aos extremos sinalizam posicionamento abarrotado que frequentemente
  precede uma reversão; o relatório marca um **extremo de compra** (≥80) ou **extremo de venda** (≤20).
- **Correção ponto no tempo.** Um relatório semanal é medido numa terça-feira, mas fica público apenas na
  sexta-feira seguinte; cada leitura honra esse instante de lançamento, então um sinal de posicionamento
  em backtest nunca vê um relatório antes de sua publicação (sem look-ahead).

## Usando a página

Abra **Commitment of Traders** na navegação esquerda. Escolha um **mercado**, um **tipo de relatório** (Legacy /
Disaggregated / Financial) e alterne **Futuros + opções** para alternar entre apenas futuros e a variante combinada.
A página mostra:

- **Posicionamento líquido ao longo do tempo** — um gráfico de linhas interativo da posição líquida (comprado − vendido)
  de cada categoria de trader na janela de histórico.
- **Índice COT** — um gráfico de linhas do índice 0–100, com a leitura mais recente e seu rótulo extremo.
- **Snapshot mais recente** — uma tabela de comprado / vendido / líquido / % de interesse em aberto por categoria
  de trader, mais interesse em aberto total e data do relatório.

Cada gráfico possui botões de barra de ferramentas para **ampliar / reduzir** (e resetar), e você pode arrastar ao longo do eixo de tempo para ampliar. **Exportar CSV** baixa o histórico completo semanalmente do mercado selecionado e tipo de relatório como arquivo pronto para planilha. Use **Comparar mercados** para sobrepor vários mercados em um único gráfico — os gráficos de comparação plotam a posição líquida especulativa de cada mercado selecionado e o índice COT lado a lado, para que você possa ler o posicionamento entre mercados em um relance.

## Como os dados fluem

O banco de dados é o cache. Um trabalhador de ingestão semanal extrai os seis conjuntos de dados CFTC dos mercados rastreados, faz upsert do catálogo de mercado e anexa cada novo relatório **idempotentemente** (re-executar nunca duplica um snapshot). Além disso, os dados são **carregados sob demanda**: a primeira vez que um mercado é solicitado, ele é obtido da fonte CFTC e armazenado, e cada solicitação subsequente é atendida diretamente do banco de dados. O cache **se atualiza conforme novos relatórios semanais são lançados** — uma vez que o relatório armazenado mais novo tenha mais de uma semana de idade, a próxima solicitação extrai transparentemente e anexa os dados mais recentes (limitado para que a fonte nunca seja assoberbada). O primeiro carregamento preenche vários anos de histórico; uma interrupção de fonte degrada para atender os melhores dados em cache. Tudo funciona fora da caixa sem chave; um token de aplicativo Socrata opcional apenas aumenta o limite de taxa.

## Configuração

Todas as chaves estão em `App:Cot` (veja [alternadores de recursos](./feature-toggles.md) e
[configurações de proprietário white-label](./white-label-owner-settings.md)):

| Chave | Padrão | Propósito |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Se o trabalhador de ingestão semanal é executado. |
| `PollInterval` | `6h` | Com que frequência o trabalhador pesquisa os conjuntos de dados CFTC. |
| `BackfillYears` | `5` | Anos de histórico extraído na primeira execução. |
| `ReconcileLookbackWeeks` | `4` | Semanas recentes ressincronizadas a cada ciclo para pegar revisões. |
| `SocrataAppToken` | — | Token opcional que aumenta o limite de taxa anônima. |
| `CotIndexLookbackWeeks` | `156` | Relatórios semanais usados como intervalo de índice COT (~3 anos). |

## Gating

A visibilidade é uma porta de dois níveis, idêntica ao calendário econômico: a porta rígida white-label
`App:Branding:EnableCot` (nível de construção) **e** o alternador de recursos de tempo de execução `App:Features:Cot`.
Com qualquer um desligado, o link de navegação, página, API REST e ferramentas MCP desaparecem (a API retorna `404`).
Como a fonte de dados é sem chave, não há gate de chave de fonte de dados — habilitado significa visível.

## Para desenvolvedores

- Domínio: `Core.Cot` — agregados `CotMarket` e `CotReport`, objeto de valor `CotPositions`, serviço de domínio
  `CotIndexCalculator` e portas `ICotReports` / `ICotSource`.
- Infraestrutura: `Infrastructure.Cot` — analisador anti-corrupção `CftcSocrataSource`, portão de taxa, serviço
  de escrita somente anexo, lado de leitura e trabalhador de ingestão semanal (esquema EF `cot`).
- Acesso de cBot & IA: a [COT cBot API](./cot-cbot-api.md) (REST, `market:read` JWT) e ferramentas MCP
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
