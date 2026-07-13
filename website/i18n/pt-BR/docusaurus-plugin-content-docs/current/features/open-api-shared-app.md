---
description: "Envie um aplicativo Open API cTrader para cada usuário (modo compartilhado com marca branca), uma única URL de redirecionamento para registrar e limites de taxa de cliente por tipo de mensagem."
---

# Aplicativo Open API compartilhado e limites de taxa

Por padrão, cada usuário registra seu próprio **aplicativo cTrader Open API** sob **Settings → Open API**. Um operador com marca branca (tipicamente um broker ou revendedor cTrader) pode, em vez disso, enviar **um aplicativo Open API compartilhado para todos os usuários** — ninguém registra o seu; todos autorizam suas contas através do aplicativo único do operador.

## Duas maneiras de fornecer o aplicativo compartilhado

O aplicativo compartilhado é provisionado tanto da configuração de implantação **quanto** da interface de configurações do proprietário (o valor definido pelo proprietário ganha). Forneça uma vez e o modo compartilhado se ativa para todos.

### 1. Configuração de implantação (semeada na inicialização)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // URL pública canônica desta implantação
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // encriptado em repouso; nunca registrado
    }
  }
}
```

Na inicialização o aplicativo semeia um aplicativo compartilhado possuído pela conta de proprietário (idempotente — nunca sobrescreve um valor de tempo de execução editado pelo proprietário e semear novamente é um não-op).

### 2. Configurações de proprietário (tempo de execução, sem reimplantação)

**Settings → Open API** (somente proprietário) mostra um cartão **Aplicativo compartilhado de implantação**: adicione / edite / delete o aplicativo compartilhado, com a URL de redirecionamento exibida para copiar e colar. As mudanças entram em vigor para novas autorizações imediatamente.

## A URL de redirecionamento (registre isto em cTrader)

Cada aplicativo Open API cTrader registra **uma** URL de redirecionamento — o **mesmo valor único** para o aplicativo compartilhado e para qualquer aplicativo por usuário:

```
{sua url de implantação}/openapi/callback
```

por exemplo `https://cmind.yourbroker.com/openapi/callback`.

- O aplicativo **exibe o valor exato** na página de configurações de Open API (com um botão de cópia) — cole-o no portal de parceiro cTrader quando você cria o aplicativo Open API.
- É composto a partir de `App:OpenApi:PublicBaseUrl` para que fique estável atrás de um proxy reverso / CDN; quando não definido, volta ao host da solicitação de entrada.
- A experiência de convite vs usuário normal difere apenas em onde o usuário desembarca **após** o callback (lista de contas vs confirmação de "contas adicionadas") — a URL de redirecionamento registrada permanece inalterada.

## O que os usuários veem em modo compartilhado

Quando um aplicativo compartilhado existe:

- Os usuários **não têm opção** de registrar seu próprio aplicativo Open API — a página de configurações mostra **"Open API é gerenciado por seu provedor"** e um botão **Autorizar contas** que usa o aplicativo compartilhado.
- Quaisquer aplicativos pessoais pré-existentes são **removidos**; suas contas autorizadas são re-apontadas para o aplicativo compartilhado e devem ser **re-autorizadas** (seus tokens antigos foram emitidos em uma ID de cliente diferente). Tentar criar um aplicativo pessoal retorna um erro "gerenciado por seu provedor".

## Limites de taxa de cliente (por tipo de mensagem)

O cliente controla mensagens Open API cTrader de saída para que um pico nunca acione um bloqueio de limite de taxa do lado do servidor. Os limites são **por tipo de mensagem**, correspondendo aos documentos Open API cTrader:

| Categoria | O que cobre | Padrão |
|---|---|---|
| `General` | mensagens de negociação + leitura (pedidos, símbolos, consultas de conta) | 45 msg/s |
| `HistoricalData` | solicitações de trendbar / dados de tick (acelerados mais duros por cTrader) | 5 msg/s |

Uma solicitação de dados históricos conta **ambos** seu próprio balde e o balde geral. Mensagens de heartbeat e autenticação nunca são controladas. As mensagens enfileiram e drenam na taxa disponível — nada é descartado e a ordem é preservada.

Ajuste-os se seu broker negociou **maiores** limites cTrader ou defina uma categoria como **`0`** para desabilitar o controle inteiramente (ilimitado):

- **Config:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msgs/seg).
- **Configurações de proprietário:** o cartão **Limites de taxa de cliente** em **Settings → Open API** (substituição de proprietário ganha, aplica-se a novas conexões / na reconexão).
