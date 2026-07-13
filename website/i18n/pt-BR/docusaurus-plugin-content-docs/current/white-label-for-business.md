---
slug: /white-label-for-business
title: Rótulo branco para negócios
description: Envie o cMind como seu próprio produto marcado — para empresas prop, corretores e negócios de copy-trading. Remarca cada superfície via config, sem mudanças de código.
sidebar_position: 4
---

# Rótulo branco cMind para seu negócio 🏢

Executa uma empresa prop, uma mesa de corretor ou um serviço de copy-trading? cMind foi construído desde o primeiro dia para ser
**revendido como seu próprio produto**. Cada superfície — o nome, o logotipo, o favicon, as cores, até
o aplicativo de telefone instalável — se dobra à sua marca. Seus clientes veem *sua* empresa. Nenhuma mudança de código,
nenhuma bifurcação, apenas config.

:::tip TL;DR
Aponte `App:Branding` para seu nome, cores e logotipo. Reinicie. Pronto. Referência técnica completa vive
na [documentação de recurso de Rótulo Branco](./features/white-label.md).
:::

## O que você pode remarca

| Superfície | O que muda |
|---|---|
| **Nome do produto** | Texto da barra de aplicativos + título da guia do navegador |
| **Logo & favicon** | Suas marcas em todos os lugares, incluindo a guia do navegador |
| **Cores** | Paleta completa — primária, superfícies, cores de status — flui por toda a interface *e* CSS próprio do aplicativo via tokens de design |
| **Aplicativo instalável (PWA)** | O nome de adicionar à tela inicial, ícone e splash usam sua marca |
| **Meta / SEO** | Descrição e URL de suporte são seus |
| **CSS personalizado** | Injete seu próprio polimento para o último 5% |

Tudo padrão para a identidade de cMind de estoque, então você só substitui o que se importa.

## O rebrand de 60 segundos

Defina estes em sua implantação (config JSON ou variáveis de ambiente):

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

Forma de variável de ambiente: `App__Branding__ProductName=AcmeFX`. As cores são validadas na inicialização —
um valor hex ruim falha a inicialização com uma mensagem clara em vez de renderizar uma página quebrada. Legal e
alto, exatamente quando você quer.

## O link "Powered by cMind"

Por **padrão**, o painel mostra um pequeno e elegante **"Powered by cMind"** link que
aponta visitantes de volta para este site. Está ativado por padrão porque somos orgulhosos do projeto e
ajuda outros traders a encontrá-lo — mas é **sua decisão**.

- **Mantenha-o** (padrão): um link de crédito sutil no painel. Não custa nada, ajuda o projeto.
- **Oculte-o**: defina `App__Branding__ShowSiteLink=false` e desaparece inteiramente — perfeito para um
  implantação totalmente com marca branca onde o produto é indubitavelmente *seu*.

Veja a [documentação de recurso de Rótulo Branco](./features/white-label.md#powered-by-link) para exatamente onde ele
renderiza.

## Multi-locatário, marca por cliente

Porque marca é apenas config de implantação, cada implantação de locatário pode carregar sua própria identidade. Executa um
instância separada por cliente, ou dirija marca a partir de seu próprio plano de controle — o aplicativo lê de
`IOptionsMonitor`, então pode até reconstruir o tema ao vivo quando as opções mudam.

Combine isto com:

- **[Alternâncias de características](./features/feature-toggles.md)** — decidir quais capacidades cada locatário vê.
- **[Regras prop-firm](./features/prop-firm.md)** — cumprir suas regras de desafio com rastreamento de patrimônio ao vivo.
- **[Taxas de desempenho](./features/copy-performance-fees.md)** + **[marketplace de provedores](./features/copy-provider-marketplace.md)** — monetizar copy trading.
- **[Conformidade](./features/compliance.md)** — manter a trilha de auditoria que seu regulador pedirá.

## Ativos & hospedagem

Solte seu logotipo/favicon no `wwwroot/branding/` do aplicativo Web (ou aponte `LogoUrl`/`FaviconUrl`
em qualquer URL absoluta). Implante da forma que for adequada — [Docker](./deployment/local.md),
[Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md), ou
[AWS](./deployment/cloud-aws.md).

Pronto para torná-lo seu? Comece com a [referência técnica de rótulo branco →](./features/white-label.md)
