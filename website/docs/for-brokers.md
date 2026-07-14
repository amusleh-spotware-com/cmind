---
slug: /for-brokers
title: cMind for cTrader brokers
description: Why a cTrader broker should run a white-label cMind for its own clients — give traders AI, copy trading and prop-firm challenges under your brand, restrict accounts to your brokerage, and win an edge over competitors.
keywords:
  - cTrader broker
  - white-label trading platform
  - broker technology
  - copy trading for brokers
  - AI trading tools
  - prop firm software
sidebar_position: 6
---

# cMind for cTrader brokers 🏦

You run a cTrader brokerage. Your clients can already trade — but so can every other broker's
clients. **cMind lets you hand your traders a full AI-powered trading operations platform, branded as
your own**, so they build, backtest, run, copy, and monitor strategies inside *your* ecosystem
instead of drifting to a third-party tool. That's stickier clients, more volume, and a real edge over
brokers offering nothing but a terminal.

:::tip[TL;DR]
Run a white-label cMind for your clients. Restrict accounts to **your** brokerage, switch on AI and
copy trading, and ship it under your brand. → [White-label for business](./white-label-for-business.md)
:::

## The edge you get over other brokers

- **Differentiate on tooling, not just spreads.** Give clients AI cBot generation, backtesting on a
  managed cluster, copy trading, and prop-firm challenges — capabilities most brokers simply don't
  offer.
- **Keep clients in your ecosystem.** When traders build and run their strategies inside your branded
  platform, they stay. Retention is the whole game.
- **Under your brand, on your domain.** Name, logo, colors, favicon, even the installable phone app —
  all yours. Nobody sees "cMind." → [White-label feature](./features/white-label.md)

## Serve only your accounts (broker allowlist)

Running a white-label for *your* clients? Restrict which brokers' trading accounts users may add so
your deployment only ever serves your book:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Your Brokerage Name"]
    }
  }
}
```

When the allowlist is set, cMind checks every account a user tries to add — both via the cTrader Open
API and via manual cID login (verified by reading the account's real broker name) — and rejects any
account that isn't on your list. Leave it empty and every broker is allowed (the default). See the
[White-label feature doc](./features/white-label.md#broker-allowlist) for the full mechanics.

## Ship one Open API app for all your users

Skip the per-user hassle: provide **one cTrader Open API application** and every client authorizes
their accounts through it — no client ever registers their own. Register a single redirect URL, drop
the credentials in config or the owner settings, and shared-mode turns on for everyone. Negotiated a
higher cTrader message limit? Tune the **per-message-type client rate limits** (or disable pacing).
→ [Shared Open API application & rate limits](./features/open-api-shared-app.md)

## New ways to monetize

- **AI, with zero friction for clients.** Provide a default AI provider key at the deployment level and
  every client gets AI features instantly — no signup elsewhere. Mark it up, or bundle it into premium
  tiers. Clients can still bring their own key. → [AI feature](./features/ai.md)
- **Prop-firm challenges.** Run funded-trader challenges with live equity tracking and enforced rules,
  and charge for entries. → [Prop-firm rules](./features/prop-firm.md)
- **Copy-trading business.** Performance fees and a provider marketplace turn copy trading into
  revenue. → [Performance fees](./features/copy-performance-fees.md) ·
  [Provider marketplace](./features/copy-provider-marketplace.md)
- **Feature tiers.** Decide which capabilities each client segment sees with
  [feature toggles](./features/feature-toggles.md).

## Regulated, auditable, multi-tenant

- **[Compliance](./features/compliance.md)** logs give you the audit trail your regulator will ask for.
- **[Two-factor auth](./features/two-factor-auth.md)** can be made mandatory per deployment.
- **Per-client branding** — run a separate branded instance per segment, driven from your own control
  plane. → [Multi-tenant branding](./white-label-for-business.md#multi-tenant-per-customer-branding)

## How to get started

1. Read [White-label for business](./white-label-for-business.md) for the 60-second rebrand.
2. Set `App:Accounts:AllowedBrokers` to your brokerage and pick your [feature set](./features/feature-toggles.md).
3. [Deploy](./deployment/cloud.md) it — Docker, Kubernetes, Azure, or AWS.

Don't want to run the infrastructure yourself? A hosting provider can operate a managed cMind for you
— point them at [For cloud & VPS providers](./for-cloud-providers.md).

## Shape the roadmap

cMind is open source. Brokers who build on it get an outsized say in where it goes — request the
integrations and controls you need, and contribute them back via the
[Contributing guide](./contributing.md).
