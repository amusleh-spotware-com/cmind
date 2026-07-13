---
description: "cMind menghantar pelayan Model Context Protocol (MCP) sebagai proses/Penempatan yang terpisah — skala + sebarkan semula bebas apl Web. Dedahkan cBot, contoh, alat AI…"
---

# Pelayan MCP

cMind menghantar pelayan Model Context Protocol (MCP) sebagai **proses/Penempatan yang terpisah** — skala + sebarkan semula bebas daripada apl Web. Dedahkan cBot, contoh, alat AI kepada klien MCP (cth pembantu AI) melalui pengangkutan HTTP + SSE.

## Auth

- Kunci API setiap pengguna `mcpk_<hex>`, SHA-256 dicincang, indeks awalan (`McpKeyAuthHandler`). Uruskan daripada halaman **Mcp** (agregat `McpApiKey`).
- Pengangkutan HTTP tanpa keadaan dengan `AddHttpContextAccessor` — panggilan alat berjalan sebagai pengguna authed.

## Alatan

- `CBotTools` — pengarang / binaan cBots.
- `InstanceTools` — jalankan / ujian belakang / pemeriksaan contoh.
- `AiTools` — janakan, ulasan, sentimen, analisis-ujian belakang, alatan salinan.

## Ops

Dedahkan `/version`; titik akhir kesihatan (`/health`, `/alive`) dipetakan semua persekitaran untuk siasatan K8s/cloud. Serilog JSON berstruktur + OpenTelemetry, sama seperti apl Web.
