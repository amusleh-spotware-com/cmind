---
description: "Catálogo de configuração para cada provedor de IA que cMind suporta — Anthropic, OpenAI, Azure OpenAI, Google Gemini e cada ponto de extremidade compatível com OpenAI incluindo modelos locais (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) e nuvens compatíveis com OpenAI."
---

# Provedores de IA — catálogo de configuração

A camada IA do cMind é agnóstica do provedor (veja [recursos de IA](../features/ai.md)). Configure um provedor de duas
formas:

1. **Interface (proprietário):** Configurações → IA → **Adicionar provedor** → escolha tipo, URL base, modelo, chave (opcional para
   local), alternâncias de capacidade, **Definir ativo** → **Testar conexão**.
2. **Config/env (ops):** semear `App:Ai:Providers[]` e `App:Ai:ActiveProvider` — importado para a loja
   na primeira inicialização quando nenhuma credencial existe. Exemplo (env, índice de provedor `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (omita para pontos de extremidade locais sem chave)
   ```

Exatamente um provedor está ativo por vez. As chaves são armazenadas criptografadas; um ponto de extremidade local não precisa de nenhum.

## Segurança: http vs https

`http://` simples é aceito **apenas** para hosts loopback / privado (intranet) — o caso de LLM local
(Ollama, LM Studio, vLLM, uma caixa on-prem). Qualquer host roteável na internet pública **deve** ser
`https://`, para que uma chave de API nunca seja enviada em texto puro. Air-gapped/on-prem: aponte a URL base para seu
ponto de extremidade interno (loopback ou IP privado) e deixe a chave em branco se o tempo de execução não autenticado.

## IA local integrada (ONNX, enviado)

cMind envia um **LLM local real em processo** (Microsoft.ML.OnnxRuntimeGenAI) que é **habilitado por
padrão** — sem chave, sem serviço externo. Na primeira inicialização, quando nenhum provedor é configurado e
`App:Branding:AllowBuiltInAi` é `true`, é semeado e ativado automaticamente.

- **Config:** `App:Ai:BuiltIn:Enabled` (padrão `true`), `App:Ai:BuiltIn:ModelPath` (padrão
  `models/onnx`, relativo ao diretório base do app), `App:Ai:BuiltIn:MaxTokens` (padrão `1024`).
- **Arquivos de modelo:** aponte `ModelPath` para um diretório contendo um modelo ONNX GenAI — `genai_config.json`,
  o tokenizador e os pesos `.onnx`. Uma compilação CPU **Phi-3-mini** funciona bem, por exemplo:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # depois defina App:Ai:BuiltIn:ModelPath para essa pasta (contém genai_config.json)
  ```

  Agrupe a pasta com sua imagem de implantação / volume Helm, ou monte-a no tempo de execução. Quando os arquivos estão
  ausentes a integrada se degrada para uma mensagem clara "model not installed" — o app ainda funciona; configure
  outro provedor ou instale o modelo.
- **GPU:** troque o pacote/modelo CPU por uma compilação ONNX GenAI CUDA/DirectML; o caminho do código é inalterado.

## Rótulo branco: limitando IA

Defina em `App:Branding` (imposto no servidor — um upsert proibido retorna `400`):

- `AllowBuiltInAi: false` — remove o modelo integrado enviado inteiramente.
- `AllowLocalProviders: false` — proíba pontos de extremidade locais/auto-hospedados (Ollama/LM Studio/vLLM e qualquer
  URL loopback/privado compatível com OpenAI).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — permita apenas esses tipos (vazio = tudo).

## Estendendo com futuros modelos integrados

A camada do provedor é baseada em adaptador (`IAiProvider` codificado por `AiProviderKind`), então um futuro tempo de execução de modelo integrado
é adicionado sem tocar qualquer característica de IA: adicione um tipo, implemente um adaptador, registre-o. O
integrado ONNX é a implementação de referência. Veja [Recursos de IA → Estendendo](../features/ai.md#extending-future-built-in-models).

## Provedores em nuvem

### Anthropic (Claude)

- Chave: <https://console.anthropic.com/> → Chaves de API.
- URL base: `https://api.anthropic.com/` · Modelo: por exemplo `claude-opus-4-8`.
- Capacidades: busca na web + visão ativada por padrão.

### OpenAI

- Chave: <https://platform.openai.com/api-keys>.
- URL base: `https://api.openai.com/v1/` · Modelo: por exemplo `gpt-4o`.
- Tipo: **OpenAiCompatible**. Ativar visão no diálogo se usar modelo de visão.

### Azure OpenAI

- Chave + ponto de extremidade: portal Azure → seu recurso Azure OpenAI.
- URL base: `https://<resource>.openai.azure.com/` · Modelo: seu **nome de implantação**.
- Tipo: **AzureOpenAi** (usa o cabeçalho `api-key` + consulta `api-version` e o caminho de implantação).

### Google Gemini

- Chave: <https://aistudio.google.com/app/apikey>.
- URL base: `https://generativelanguage.googleapis.com/` · Modelo: por exemplo `gemini-2.0-flash`.
- Tipo: **Gemini**. Fundamentação de busca na web + visão ativada por padrão.

### Outras nuvens compatíveis com OpenAI (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Tipo: **OpenAiCompatible**. URL base = ponto de extremidade compatível com OpenAI do provedor, Modelo = seu id de modelo,
  ApiKey = chave do provedor. Nenhuma mudança de cMind necessária — um adaptador os atende a todos.

## Modelos locais (sem chave)

Todos os tempos de execução locais expõem o fio OpenAI Chat Completions, então use **Tipo: OpenAiCompatible** com
URL base do tempo de execução e nome do modelo servido; deixe a chave em branco.

### Ollama

```
# instale de https://ollama.com, então:
ollama pull llama3.1:8b
```

- URL base: `http://localhost:11434/v1/` · Modelo: o nome puxado (por exemplo `llama3.1:8b`, `qwen2.5-coder`).
- Sem chave de API. Capacidades padrão apenas para texto; ativar visão apenas para um modelo de visão.

### LM Studio

- Inicie o servidor local (Desenvolvedor → Iniciar servidor).
- URL base: `http://localhost:1234/v1/` · Modelo: id de modelo carregado. Sem chave de API.

### vLLM / llama.cpp `server` / LocalAI

- Serve um ponto de extremidade compatível com OpenAI (cada um envia um).
- URL base: seu URL servido (por exemplo `http://localhost:8000/v1/`) · Modelo: nome do modelo servido. Sem chave
  a menos que você coloque autenticação na frente.

## Verificação

- **Testar conexão** no diálogo executa uma pequena conclusão de ping e relata sucesso + latência — ideal
  para confirmar um ponto de extremidade local.
- Automatizado: a suite E2E do aplicativo conduz cada característica de IA contra um servidor fake OpenAI-compatível em processo
  por padrão, ou seu provedor real quando `AI_E2E_BASEURL` (+ opcional `AI_E2E_API_KEY` /
  `AI_E2E_KIND` / `AI_E2E_MODEL`) é definido. Veja [Recursos de IA → Teste](../features/ai.md#testing-with-the-fake-local-llm).

## Alternância / rotação

- **Provedor ativo de alternância:** Configurações → IA → **Definir ativo** em outro cartão (ativar um desativa
  o resto).
- **Girar uma chave:** edite o provedor e forneça uma nova chave (deixe em branco para manter a armazenada).
- **Remover:** delete o cartão. Sem provedor ativo, recursos de IA se desabilitam e o resto do app funciona
  inalterado.
