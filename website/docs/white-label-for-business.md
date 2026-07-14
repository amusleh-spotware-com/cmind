---
slug: /white-label-for-business
title: White-label for business
description: Ship cMind as your own branded product — for prop firms, brokers, and copy-trading businesses. Rebrand every surface via config, no code changes.
sidebar_position: 4
---

# White-label cMind for your business 🏢

Run a prop firm, a broker desk, or a copy-trading service? cMind was built from day one to be
**resold as your own product**. Every surface — the name, the logo, the favicon, the colors, even
the installable phone app — bends to your brand. Your customers see *your* company. No code changes,
no fork, just config.

:::tip[TL;DR]
Point `App:Branding` at your name, colors, and logo. Restart. Done. Full technical reference lives
in the [White-label feature doc](./features/white-label.md).
:::

## What you can rebrand

| Surface | What changes |
|---|---|
| **Product name** | App bar text + browser tab title |
| **Logo & favicon** | Your marks everywhere, including the browser tab |
| **Colors** | Full palette — primary, surfaces, status colors — flows through the whole UI *and* the app&apos;s own CSS via design tokens |
| **Installable app (PWA)** | The add-to-home-screen name, icon, and splash use your brand |
| **Meta / SEO** | Description and support URL are yours |
| **Custom CSS** | Inject your own polish for the last 5% |

Everything defaults to the stock cMind identity, so you only override what you care about.

## The 60-second rebrand

Set these on your deployment (JSON config or environment variables):

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

Environment-variable form: `App__Branding__ProductName=AcmeFX`. Colors are validated at startup —
a bad hex value fails the boot with a clear message instead of rendering a broken page. Nice and
loud, exactly when you want it.

## The &quot;Powered by cMind&quot; link

By **default**, the dashboard shows a small, tasteful **&quot;Powered by cMind&quot;** link that
points visitors back to this site. It&apos;s on by default because we&apos;re proud of the project and
it helps other traders find it — but it&apos;s **your call**.

- **Keep it** (default): a subtle credit link on the dashboard. Costs you nothing, helps the project.
- **Hide it**: set `App__Branding__ShowSiteLink=false` and it disappears entirely — perfect for a
  fully white-labeled deployment where the product is unmistakably *yours*.

See the [White-label feature doc](./features/white-label.md#powered-by-link) for exactly where it
renders.

## Multi-tenant, per-customer branding

Because branding is just deployment config, each tenant deployment can carry its own identity. Run a
separate instance per customer, or drive branding from your own control plane — the app reads it from
`IOptionsMonitor`, so it can even rebuild the theme live when options change.

Pair this with:

- **[Feature toggles](./features/feature-toggles.md)** — decide which capabilities each tenant sees.
- **[Prop-firm rules](./features/prop-firm.md)** — enforce your challenge rules with live equity tracking.
- **[Performance fees](./features/copy-performance-fees.md)** + **[provider marketplace](./features/copy-provider-marketplace.md)** — monetize copy trading.
- **[Compliance](./features/compliance.md)** — keep the audit trail your regulator will ask for.

## Assets & hosting

Drop your logo/favicon into the Web app&apos;s `wwwroot/branding/` (or point `LogoUrl`/`FaviconUrl`
at any absolute URL). Deploy however suits you — [Docker](./deployment/local.md),
[Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md), or
[AWS](./deployment/cloud-aws.md).

Ready to make it yours? Start with the [technical white-label reference →](./features/white-label.md)
