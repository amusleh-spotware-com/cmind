---
id: white-label-owner-settings
title: Opcoes de white-label nas Configuracoes do Owner
sidebar_label: Configuracoes de white-label do owner
---

# Opcoes de white-label nas Configuracoes do Owner

Cada opcao white-label que um deployment pode definir atraves de configuracao (`appsettings`/env) e **tambem
configuravel em runtime pelo dono do app**, em **Settings → Deployment**, sem um redeploy. Um override do owner
**vice sobre a configuracao**; limpa-lo reverte a opcao para o valor configurado do deployment (ou
default built-in).

Isso espelha como uma configuracao de deployment white-label configura o produto — as mesmas knobs, o mesmo efeito —
entao um operador pode ajustar branding, gates e politica live e ver o resultado imediatamente.

## Onde vive

- **UI:** a secao **Deployment** owner-only no dialogo de configuracoes, e a pagina deep-linkable
  **`/settings/deployment`**. Opcoes estao agrupadas em **uma aba por categoria** (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, com um dialogo windowed
  em desktop e uma superficie full-screen em telefones.
- **API:** `/api/whitelabel` (owner-only, nunca controlado por funcionalidade):
  - `GET /api/whitelabel` — toda opcao com seu valor efetivo, proveniencia (`Config` / `Owner` /
    `Default`) e se um override esta definido. **Secrets sao mascarados** (valor nunca retornado).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — define um override (validado por tipo de opcao). Um valor em branco em uma **secret** mantem a secret existente.
  - `DELETE /api/whitelabel/{key}` — limpa um override (reverte para config).
  - `POST /api/whitelabel/reset` — limpa **todos** os overrides (reverte o deployment para config pura).

## Como overrides tomam efeito

Overrides do owner sao armazenados como linhas `AppSetting` criptografadas onde necessario e empilhadas em cima do
`AppOptions` bound por um `IOptionsMonitor<AppOptions>` decorado. Porque todo consumidor ja le opcoes
através daquele monitor, um override aplica **live** atraves de todo o app — o tema, titulo da pagina, gate MFA,
gates de provedor AI, lista de brokers permitida, politica de registro, configuracoes de transporte de email, etc. atualizam
na proxima leitura (tema/branding rerenderizam imediatamente). Se o banco de dados esta brevemente indisponivel a camada
**falha aberta** para a linha de base configurada, entao uma leitura de override nunca pode quebrar o app.

**Feature flags** fazem parte da mesma superfice mas sao persistidos atraves da store de override de feature existente
(`IFeatureGate`), entao a aba Features e os toggles de feature isolados nunca divergem.

**Secrets** (SMTP password, CAPTCHA secret, provisioning secret) sao criptografados em repouso
(`ISecretProtector`, purpose `whitelabel.secret`), write-only na UI, e nunca retornados pela API.

## Opcoes delegadas

As credenciais da **aplicacao Open API compartilhada** e **limites de taxa por tipo de mensagem** sao gerenciados na
**secao de configuracoes Open API** (veja os docs de copy-trading / Open API). Eles aparecem no catalogo de Deployment
como entradas *delegadas* (somente leitura aqui, com um link) para que nada seja duplicado e a garantia de sync ainda os conta como cobertos.

## Sempre em sync (aplicado)

Adicionar uma nova opcao white-label a configuracao **deve** surfacea-la nas configuracoes do owner no mesmo
commit. Isso e aplicado por `WhiteLabelCatalogParityTests`: ele reflete sobre cada propriedade de registro de opcoes
white-label e falha o build a menos que a propriedade esteja registrada em
`Core/WhiteLabel/WhiteLabelCatalog` (ou explicitamente listada em `IntentionallyExcluded` com uma razao).
Veja o mandato 10 em `CLAUDE.md`.

## Notas

- Habilitar SMTP em um deployment que comecou com **nenhum** email configurado precisa de um restart (o tipo de sender
  e escolhido na inicializacao); host/credenciais de um sender ja configurado atualizam live.
- **Labels/descriptions** de opcoes sao identificadores tecnicos de knob de config mostrados como dados; os labels das abas e
  todo chrome interativo sao totalmente localizaveis.
