---
title: 0004 — CBotBuilder runs on the web host in a sandbox container
description: Why untrusted cBot builds happen on the web host inside a throwaway SDK container rather than on a node.
---

# 0004 — `CBotBuilder` runs on the web host in a sandbox container

## Context

Building a user's cBot means running **untrusted MSBuild** — arbitrary code at build time (targets,
source generators, restore scripts). It needs the Docker socket to spin up an SDK container. Nodes
run trading containers and shouldn't also hold build privileges.

## Decision

`CBotBuilder` runs **on the web host** (which already has the Docker socket), inside a **throwaway SDK
container** with:

- a bind-mounted `/work` directory (only the build inputs/outputs, not the host filesystem);
- a shared `app-nuget-cache` volume for restore performance;
- no host network access beyond what restore needs.

So untrusted MSBuild can't reach the host filesystem or network. Run/backtest containers, by
contrast, run on nodes picked by `NodeScheduler`.

## Consequences

- Build privilege (Docker socket) is confined to the web host; nodes only run allowed trading images.
- Each build is isolated in a disposable container — a malicious build can't persist or escape.
- The web host must have a Docker socket available; this is a deployment requirement, not optional.
