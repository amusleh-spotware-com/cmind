---
title: Ortak Ağ Protokolü (MCP)
description: cMind araçlarını AI istemcilerine (Claude, ChatGPT) HTTP+SSE üzerinden ifşa edin.
sidebar_position: 28
---

# Ortak Ağ Protokolü (MCP) Sunucusu

cMind, AI istemcileri cBot'ları oluşturmak, parametreleri ayarlamak, stratejileri analiz etmek ve koşuları yönetmek için araç sağlayan bir MCP sunucusu çalıştırır.

## MCP Araçları

- **CreateCBot**: Uyarı → C# cBot kodu
- **BacktestStrategy**: Tarih, sembol, parametreler → PnL raporları
- **TuneBotParameters**: Backtest verileri → Optimal parametreler
- **GetBotMetrics**: Çalışan bot → KPI'lar (Sharpe, MDD)
- **ListCopyProfiles**: Kurulu kopya profillerini listele

## Bağlantı

AI istemci (Claude, etc.):

```
MCP Server URL: http://cmind-web:8081/mcp
Transport: HTTP + SSE
```

Claude:

```json
{
  "resources": [
    {
      "uri": "http://cmind-web:8081/mcp",
      "name": "cMind"
    }
  ]
}
```

Daha fazla: [AI Çekirdeği →](./ai.md)
