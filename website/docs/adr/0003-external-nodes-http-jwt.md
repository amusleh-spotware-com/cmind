---
title: 0003 — External nodes are HTTP + JWT, no SSH/shell
description: Why remote node agents expose only an HTTP API with short-lived JWTs and never a shell.
---

# 0003 — External nodes are HTTP + JWT, no SSH/shell

## Context

Backtest/run containers execute on remote hosts. The obvious approach — SSH in and run docker — gives
the main app arbitrary remote code execution and long-lived credentials on every node. That is a
large blast radius for a system that runs untrusted user cBots.

## Decision

Each remote host runs a standalone `ExternalNode` **HTTP agent** with **no SSH and no shell**. The
main app calls the agent over HTTP; every request carries a short-lived **HS256 JWT** (5-minute,
`iss=app-main` / `aud=app-node`) signed with that node's secret. The agent:

- only runs images matching `AllowedImagePrefix` (with a path boundary so `ghcr.io/spotware` can't
  match `ghcr.io/spotware-evil/...`);
- execs docker via `ArgumentList` — never a shell string;
- is **stateless**, finding containers by the `app.instance` label;
- self-registers and heartbeats to `POST /api/nodes/register`; the main app upserts the `RemoteNode`
  **by name**, so a node survives IP changes.

## Consequences

- A leaked request token expires in minutes; there is no standing shell credential to steal.
- The agent's capability is bounded to "run an allowed image" — it can't be turned into a general
  remote shell.
- Node identity is name-based, so re-provisioning a node with a new IP doesn't orphan its history.
