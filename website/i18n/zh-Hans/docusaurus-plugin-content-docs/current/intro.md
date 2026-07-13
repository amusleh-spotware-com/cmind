---
slug: /intro
title: 欢迎使用 cMind
description: cMind 的友好入门介绍——面向 cTrader 的开源、可自托管的交易运营平台。
sidebar_position: 1
---

# 欢迎使用 cMind 👋

:::warning Alpha 软件——尚未准备好用于生产环境
cMind 正处于积极开发阶段。预期会有粗糙之处、版本间的破坏性变更，以及仍在开发中的功能。
**我们需要社区测试者、漏洞报告者和早期贡献者**来帮助塑造它。如果遇到问题，
[请提交报告](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) ——
你的真实世界反馈是你现在能够贡献的最有价值的东西。
:::

所以，你想构建交易机器人、在不烧坏笔记本的情况下回测它们、在多台机器上运行它们、把交易镜像到十几个账户，
并让 AI 在你睡觉时盯着风险。**你来对地方了。**

cMind 是一个**面向 cTrader 的开源、可自托管的交易运营平台**。可以把它看作你的整个交易台——编写、执行、
计算集群、跟单交易，以及一个 AI 内核——全部装进一个宁静、深色、适配移动端的应用里，从头到尾归你所有。

:::tip 一句话概括
在你自己的服务器上、以你自己的品牌，内置 AI，大规模地 构建 → 回测 → 运行 → 复制 你的 cTrader 策略。
:::

## 它到底能做什么？

| 你想要… | cMind 帮你做到 | 了解更多 |
|---|---|---|
| 在浏览器里编写 cBot | Monaco IDE + C#/Python 模板，沙箱化构建 | [构建与回测](./features/build-and-backtest.md) |
| 跨机器回测 | 自愈的节点集群挑选最空闲的机器 | [扩展](./deployment/scaling.md) |
| 把一个账户复制到多个 | 稳健的镜像，带重新同步，不会重复下单 | [跟单交易](./features/copy-trading.md) |
| 让 AI 干苦力活 | 策略生成、自我修复、风险守卫、事后复盘 | [AI 内核](./features/ai.md) |
| 遵守自营公司规则 | 实时净值跟踪 + 挑战规则模拟 | [自营公司](./features/prop-firm.md) |
| 验证回测优势 | PSR / DSR / t 统计过拟合校正 | [回测完整性实验室](./features/backtest-integrity.md) |
| 了解自己的交易习惯 | 行为漏洞检测 + AI 教练 | [交易日志](./features/trading-journal.md) |
| 为策略追踪宏观事件 | 时间点日历、新闻封锁、cBot API | [经济日历](./features/economic-calendar.md) |
| 评估货币宏观强度 | 所有货币对的 AI 前景展望 | [货币强度](./features/currency-strength.md) |
| 使用 2FA 保护账户 | TOTP 身份验证器应用 + 备用代码 | [双因素身份验证](./features/two-factor-auth.md) |
| 让所有者在运行时调整 | 每个白标选项在设置 → 部署中实时生效 | [所有者设置](./features/white-label-owner-settings.md) |
| 以任何语言运行 | 23 种语言包括 RTL——缺少键时构建失败 | [本地化](./features/localization.md) |
| 作为*你的*产品发布 | 完整白标：名称、配色、徽标、favicon | [白标](./features/white-label.md) |
| 在手机上运行 | 可安装的移动优先 PWA | [PWA](./features/pwa.md) |
| 从 AI 客户端驱动 | 内置 MCP 服务器（HTTP + SSE） | [MCP](./features/mcp.md) |

## 5 分钟上手路径 ⏱️

如果你有 Docker 和五分钟，现在就能开始把玩一个真实的 cMind 实例：

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

然后打开 **<http://localhost:8080>**，登录，就可以开始了。完整的操作指南（包含 Docker 难免有脾气时的
故障排查）见 **[在本地运行](./deployment/local.md)**。

## 新来的？沿着黄砖路走 🟡

1. **[这是给谁用的？](./audience.md)** —— 确认你是我们这类人。
2. **[在本地运行](./deployment/local.md)** —— 启动一个真实实例。
3. **[功能](./features/README.md)** —— 完整浏览内部的一切。
4. **[正式部署](./deployment/cloud.md)** —— Docker、Kubernetes、Azure、AWS。
5. **[打造成你的](./white-label-for-business.md)** —— 为你的业务贴上白标。
6. **[参与贡献](./contributing.md)** —— 非常欢迎 PR（人工*和* AI 辅助）。

## 关于钱的几句话 💸

cMind 操作**真实资金**。我们对此非常认真——每一次变更都随附单元、集成和端到端测试，包含失败路径（连接
中断、订单被拒、节点宕机）。你也应当认真对待：**先在模拟账户上测试**，在把它对准任何真实资金之前，请阅读
[合规说明](./features/compliance.md)。交易有风险；本软件是一个工具，而非投资建议。

好了——前言到此为止。我们去构建点东西吧。→
