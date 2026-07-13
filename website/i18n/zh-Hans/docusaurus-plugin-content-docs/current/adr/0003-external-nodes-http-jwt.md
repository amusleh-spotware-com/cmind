---
title: 0003 — cTrader CLI 节点为 HTTP + JWT，无 SSH/Shell
description: 为什么远程节点代理仅公开 HTTP API 和短期 JWT，从不提供 Shell。
---

# 0003 — cTrader CLI 节点为 HTTP + JWT，无 SSH/Shell

## 背景

回测/运行容器在远程主机上执行。显而易见的做法 — SSH 进去并运行 docker — 会给主应用在每个节点上提供任意远程代码执行和长期凭证的权限。对于一个运行不受信任用户 cBot 的系统来说，这是一个很大的威胁面。

## 决策

每个远程主机运行一个独立的 `CtraderCliNode` **HTTP 代理**，**无 SSH 也无 shell**。主应用通过 HTTP 调用代理；每个请求都携带一个短期 **HS256 JWT**（5 分钟，
`iss=app-main` / `aud=app-node`）由该节点的密钥签署。代理：

- 仅运行与 `AllowedImagePrefix` 匹配的镜像（具有路径边界，所以 `ghcr.io/spotware` 无法
  匹配 `ghcr.io/spotware-evil/...`）；
- 通过 `ArgumentList` 执行 docker — 从不使用 shell 字符串；
- 是**无状态的**，通过 `app.instance` 标签查找容器；
- 自注册并向 `POST /api/nodes/register` 进行心跳；主应用通过名称 upsert `CtraderCliNode`，
  所以节点在 IP 变更后仍然存活。

## 后果

- 泄露的请求令牌在几分钟内过期；没有可被盗取的长期 shell 凭证。
- 代理的能力限制在"运行允许的镜像" — 它无法被转变成通用的远程 shell。
- 节点身份基于名称，所以用新 IP 重新配置节点不会使其历史记录孤立。
