---
description: "AI do cMind e agnostic de provedor — Anthropic, OpenAI, Azure OpenAI, Google Gemini e qualquer endpoint compativel com OpenAI incluindo modelos locais (Ollama, LM Studio, vLLM). Escolha um provedor, modelo e endpoint; toda funcionalidade AI funciona inalterada."
---

# Funcionalidades AI

A camada AI do cMind e **agnostica de provedor**. Toda funcionalidade fala com uma unica interface neutra
(`IAiClient.CompleteAsync`); um **cliente de roteamento** resolve a credencial do provedor ativo e despacha
para o adaptador de wire correspondente. Voce escolhe um provedor + modelo + endpoint (e, se o provedor precisa,
uma chave); toda funcionalidade existente funciona inalterada com a mesma controladora, criptografia, resiliencia e
degradacao.

**Baterias includas:** um **LLM local built-in ships com o app e habilitado por padrao**
(Microsoft.ML.OnnxRuntimeGenAI, ex. Phi-3-mini) — entao todo deployment tem AI funcionando **sem chave de API
e sem servico externo**. Um deployment white-label pode remove-lo e restringir quais provedores usuarios podem
adicionar. Alem do built-in, conecte qualquer provedor externo.

Provedores suportados:

- **AI local built-in** (`BuiltInOnnx`) — modelo ONNX GenAI in-process, sem chave, shipped + default-on.
- **Anthropic** (Claude — Messages API)
- **OpenAI** e **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Qualquer endpoint compativel com OpenAI**, incluindo **modelos locais** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) e clouds compatíveis com OpenAI (OpenRouter, Groq, Together, Mistral,
  DeepSeek) — todos via um unico adaptador compatvel com OpenAI, diferindo apenas por base URL + modelo + chave.

Exatamente **um** provedor ativo por vez. Credenciais sao armazenadas **criptografadas**
(`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
um endpoint local precisa **sem chave**. Com **nenhum** provedor ativo, toda funcionalidade retorna o resultado disabled
e o resto do app executa inalterado (nenhuma chave necessaria para build, teste ou executar a plataforma).

**Back-compat:** um deployment existente com `App:Ai:ApiKey` legado (ou a settings encrypted `ai.api_key`
antiga) e honrado automaticamente como um provedor **Anthropic** padrao ativo — nenhuma acao necessaria.

AI nao configurada → paginas AI dim actions e mostram um banner mais um prompt unico para adicionar um provedor em
**Settings → AI** (`AiFeatureNotice`). Status em `GET /api/ai/status` (`{ enabled, kind, model }`);
provedores gerenciados (owner-only) via `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}`, e um `POST /api/ai/providers/test` connectivity ping.

## Default de deployment vs provedor proprio de um usuario

Credenciais AI tem dois escopos:

- **Default de deployment (owner-managed).** O owner configura um provedor (ou ships um via
  `App:Ai:Providers[]` / a chave `App:Ai:ApiKey` legada). Ele se torna o **default compartilhado para todo usuario** —
  entao um broker ou provedor de hosting pode financiar AI para todos seus usuarios com **sem setup por usuario e sem
  limite por usuario**. Gerenciado via as rotas owner-only `/api/ai/providers` acima.
- **Provedor proprio de um usuario (self-service).** Qualquer usuario logado pode adicionar seu proprio provedor sob
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Quando presente, **seu proprio provedor ativo sobrescreve o default de deployment
  para suas proprias funcionalidades AI**; remove-lo volta ao default.

**Ordem de resolucao** (em `AiProviderStore`, por requisicao de usuario): credencial ativa propria do usuario → o
default de deployment → a chave de config legado → nenhum (AI desabilitada). Exatamente uma credencial ativa
**por escopo** (indice unico parcial por `OwnerUserId`), e cada escopo e resolvido independentemente, entao um
usuario ativando sua propria chave nunca perturba o default compartilhado. Contextos background/nao-Web (sem requisicao
de usuario) sempre resolvem o default de deployment.

## Matriz de capacidades de provedores

Capacidades padrao por provedor e sao owner-overrideaveis. Quando uma capacidade esta off a funcionalidade
**degrada, nunca lana**: web search e silenciosamente dropado; visao retorna uma falha typed
capability-unsupported.

| Provedor | Kind | Default base URL | Chave necessaria | Web search | Visao | Notas |
|---|---|---|---|---|---|---|
| AI local built-in | `BuiltInOnnx` | n/a (in-process) | no | ✖ | ✖ | shipped ONNX GenAI model, default-on |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | yes | ✅ | ✅ | Messages API, `web_search` tool |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | yes | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | yes | ✅ | ✅ | deployment path + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | yes | ✅ | ✅ | `generateContent`, `google_search` grounding |
| Ollama (local) | `OpenAiCompatible` | `http://localhost:11434/v1/` | no | ✖ | model-dependent | via OpenAI-compatible adapter |
| LM Studio (local) | `OpenAiCompatible` | `http://localhost:1234/v1/` | no | model-dependent | model-dependent | via OpenAI-compatible adapter |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | your served URL | no | ✖ | model-dependent | via OpenAI-compatible adapter |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | provider URL | yes | ✖ | model-dependent | via OpenAI-compatible adapter |

Guias de setup por provedor (chaves, URLs, model ids, passos de UI): veja
[AI providers — catalogo de setup](../deployment/ai-providers.md).

## AI local built-in (shipped, default-on)

cMind ships um **LLM local real que executa in-process** via
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (um modelo compact instruct como
Phi-3.5-mini). Ele precisa **de nenhuma chave de API e nenhum servico externo**, e na primeira inicializacao — quando nenhum provedor e
configurado e o gate white-label permite — ele e **semeado e ativado automaticamente**, entao todo
deployment tem AI funcionando out of the box.

- O diretorio do modelo (`genai_config.json` + tokenizer + weights) e configurado por
  `App:Ai:BuiltIn:ModelPath` (padrao `models/onnx`, relativo ao diretorio base do app). Quando os arquivos do modelo
  estao ausentes o provedor **degrada para uma falha typed com uma dica de install** — nunca lana, e o resto do app e inafetado.
- Ele alimenta toda funcionalidade AI de texto. Sendo um modelo compacto, e texto-apenas (sem web search server-side ou
  visao) e geracao serializada (uma instancia de modelo, reutilizada apos lazy load).
- **Multiplos modelos built-in podem coexistir.** Cada modelo baixado vive sob `ModelPath/<key>`; um catalogo curado (Phi-3.5-mini padrao, alem de Phi-3-mini-128k) pode ser baixado e alternado a partir de **Settings → AI**. Selecionar um submodelo built-in o carrega in-process. Para adquirir/empacotar um modelo: veja [Provedores de IA → built-in](../deployment/ai-providers.md#IA-local-integrada-onnx-enviado).

## Controles white-label

Um deployment white-label restringe AI via `App:Branding` (aplicado server-side em cada upsert de provedor):

- `AllowBuiltInAi` (padrao `true`) — defina `false` para **remover o modelo built-in** inteiramente.
- `AllowLocalProviders` (padrao `true`) — defina `false` para proibir endpoints locais/self-hosted (loopback /
  OpenAI-compatível privado, ex. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (padrao vazio = todos) — liste apenas os kinds que o deployment sanciona (ex.
  `["Anthropic","OpenAiCompatible"]`) para bloquear quais provedores usuarios podem adicionar.
- `AllowAiModelManagement` (padrao `true`) — defina `false` para esconder **navegacao de modelos**, o **seletor de modelo por pagina**, e **vinculacao de modelo por funcionalidade**. Todos sao owner-ajustaveis em runtime a partir de **Settings → Deployment** (sobrescrito ao vivo em `IOptionsMonitor`) e catalogado em `WhiteLabelCatalog`.

## Extendendo: futuros modelos built-in

A camada AI e **baseada em adaptadores e construida para crescer**. Cada provedor e um `IAiProvider` selecionado por
`AiProviderKind`; a interface voltada a funcionalidade (`IAiClient`/`AiFeatureService`) nunca muda. Adicionar um novo
runtime de modelo built-in depois (outro modelo ONNX, um diferente engine in-process, GGUF/llama.cpp
in-proc, etc.) e uma mudanca localizada: adicione um `AiProviderKind`, implemente um adaptador `IAiProvider`,
registre-o, e (opcionalmente) faca wire de seed padrao + opcao de dialogo — sem mudanca em funcionalidade, endpoint ou ferramenta MCP.
O provedor ONNX built-in e a implementacao de referencia deste padrao.

## Capacidades

- **Build cBot** — prompt em ingles → cBot executavel via loop de **generate → build → AI-fix** auto-repair (`build-strategy`), em `/ai/build`. O **codigo-fonte gerado e mostrado** quando o build termina (com um botao de copiar), ao lado do log de build — em sucesso *e* em falha — entao voce sempre ve o que o AI escreveu, nao apenas erros.
- **Selecao de modelo por pagina** — toda pagina de funcionalidade AI e dialogo mostra um **seletor de modelo** listando os modelos que voce pode usar (seus proprios provedores + defaults de deployment). Ele pre-seleciona a vinculacao salva da funcionalidade se definida, caso contrario o modelo **padrao**, e o modelo que voce escolhe aplica a essa unica acao (enviado como `?modelId=` e forcado por `RoutingAiClient` para essa chamada). Escondido quando o deployment desabilita a gerencia de modelos.
- **Navegue e selecione modelos, por funcionalidade** — navegue os modelos que um endpoint de provedor anuncia (`GET /v1/models` em LM Studio / Ollama / vLLM / llama.cpp, ou o catalogo built-in) em vez de digitar um id manualmente, e **vincule cada funcionalidade AI a um modelo diferente** para que varios modelos sirvam diferentes funcionalidades ao mesmo tempo (uma funcionalidade nao vinculada retorna ao provedor padrao do escopo).
- **Otimizacao de parametros** — loop fechado: AI propoe conjuntos de params, cada um persistido + backtestado em nos (`optimize-run` / `optimize-params`).
- **Agente de portfolio autonomo** — propostas orientadas por mandato com diario de decisao completo (`AgentMandate` → `AgentProposal`).
- **Guarda de risco agindo** — servico background `AiRiskGuard` avalia cBots em execucao, pode **auto-stop** em risco critico (opt-in).
- **Guardiao de exposicao prop-firm** — limites de drawdown/exposicao com auto-flatten.
- **Alertas de mercado** — motor `AlertRule` com sentimento AI (web-search aterrissado onde o provedor suporta).
- **Analise** — revisao de cBot, analise de backtest, post-mortems, sentimento de mercado, design de visao de grafico, curadoria de marketplace.

## Superficies

- Endpoints web em `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …). Toda acao de funcionalidade AI aceita um `?modelId=<credential>` opcional para executar essa unica chamada em um modelo escolhido. Alem disso **descoberta de modelos** (`/api/ai/models/probe`, `/api/ai/usable-models`) e **vinculos por funcionalidade** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- Ferramentas MCP (`AiTools`) para clientes AI — veja [mcp.md](mcp.md). Selecao de provedor e transparente para clientes MCP.
- Grupo de nav **AI** — uma pagina Blazor **por funcionalidade**: Build cBot (`/ai/build`), Review (`/ai/review`), Debate (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Exposure Check (`/ai/exposure`), Portfolio Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimize (`/ai/optimize`), alem de Portfolio Agent, Alerts, MCP Keys. Paginas compartilham `AiFeaturePageBase` + `AiOutputPanel` + um `AiModelSelect`; cada uma mostra `AiFeatureNotice` quando nenhum provedor e configurado.
- **Settings → AI** (`/settings/ai`, owner-only) — lista de provedores com um **Add / edit provider dialog** (kind, base URL com dicas por-kind incl. um preset Ollama/LM Studio localhost, modelo, chave opcional, toggles de capacidade, "set as default") e um botao **Test connection**.

## Configuracao

`App:Ai` suporta tanto a chave unica legacy quanto o seed multi-provedor:

- Legacy: `ApiKey`, `Model` (padrao `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — ainda honrado como
  o provedor Anthropic padrao.
- Multi-provedor: `ActiveProvider` (kind) e `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — importado para a store na inicializacao se nenhuma credencial existe ainda, entao uma
  equipe de ops pode shipar um deployment configurado (incl. local-LLM) puramente via appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` inalterados. Para testes/dev, uma chave de config
vive no arquivo [dev-credentials unificado](../testing/dev-credentials.md) sob `Ai`.

## Confiabilidade

O provedor e tratado como nao confiavel — nada que ele faz pode derrubar o app. Isso se mantem identicamente
para endpoints cloud e locais (um Ollama morto retrya entao degrada exatamente como um Anthropic throttled):

- **Degradacao harmonica.** Todo modo de falha (sem provedor, HTTP 4xx/5xx/429, timeout, body malformado,
  conteudo vazio, capacidade nao suportada) retorna um `AiResult.Fail(reason)` typed — o cliente nunca
  lana em uma pagina, ferramenta MCP ou servico hospedado.
- **Pipeline de resiliencia.** `AddAiHttpClient` da ao unico `HttpClient` AI compartilhado um retry limitado em
  5xx transitarios / falhas de rede (backoff exponencial + jitter) mais timeouts generosos por tentativa e total (`AiHttp`), reutilizado por todo adaptador.

## Testando com o fake LLM local

A camada AI e provada end-to-end **sem nenhuma dependencia externa** por `FakeLocalLlmServer` — um tiny
endpoint **compatível com OpenAI** in-process retornando resposta deterministica enlatada, wire-identico a
Ollama/LM Studio/vLLM. Ele apoia:

- **Unidade** — testes de request-translation por adaptador + response-parse, roteamento/degradacao de capacidades.
- **Integracao** — o adaptador compatível com OpenAI end-to-end, a teoria de resiliencia parametrizada atraves de
  todo adaptador, e as **ferramentas AI MCP**.
- **E2E** — `AiLocalFixture` inicia o app apontado para o fake server (ou um **provedor real** quando
  o desenvolvedor define `AI_E2E_BASEURL` (+ opcional `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  credenciais reais vencem) e dirige toda funcionalidade AI atraves da UI real. Adicionar ou mudar qualquer funcionalidade AI
  **requer** um teste E2E atraves desta fixture (veja o mandato de teste do repo). Uma faixa opt-in
  (`AI_LOCAL_LLM=1`) executa uma completacao real atraves de um **Ollama** Testcontainer.

## AI local built-in — zero-setup por padrao

O LLM local ONNX built-in funciona pronto para uso: quando seu diretorio de modelo esta ausente e
`App:Ai:BuiltIn:AutoDownload` e `true` (o padrao), o app baixa o modelo uma vez em segundo plano
a partir de `App:Ai:BuiltIn:DownloadBaseUrl`. Enquanto o download executa, chamadas AI (e **Test
connection** em Settings → AI) retornam uma mensagem clara "modelo esta baixando (setup primeira-vez)" em vez de
uma falha rigida. Deployments air-gapped/medidos definem `AutoDownload=false` e
pre-provisionam o diretorio do modelo (`App:Ai:BuiltIn:ModelPath`). O gate white-label
`App:Branding:AllowBuiltInAi` ainda se aplica.

O download tambem e **pre-aquecido na inicializacao** quando o modelo built-in e o provedor ativo, entao fica pronto antes do primeiro clique AI em vez de falhar esse clique com "baixando…". **Settings → AI** expoe o estado de instalacao ao vivo no cartao do provedor built-in — *Modelo pronto* / *Baixando modelo…* / *Modelo nao instalado* / *Download falhou* — com um botao **Download model** (ou **Tentar novamente download**) que inicia o fetch em segundo plano unico sob demanda (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`). Habilitar o provedor built-in a partir de Settings reutiliza a linha ja semeada em vez de adicionar uma duplicada, entao nunca conflita na restricao de provedor-unico-ativo.
