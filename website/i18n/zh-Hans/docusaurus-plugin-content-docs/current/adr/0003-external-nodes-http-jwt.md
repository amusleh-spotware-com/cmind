---
title: 0003 — cTrader CLI 节点是 HTTP + JWT，没有 SSH/shell
description: 为什么远程节点代理仅公开 HTTP API 与短期 JWT，永远不会有 shell。
---

# 0003 — cTrader CLI 节点是 HTTP + JWT，没有 SSH/shell

## 背景

回测/运行容器在远程主机上执行。明显的方法——SSH 进去并运行 docker——给
主应用任意远程代码执行和每个节点上的长期凭证。这是一个
大的爆炸半径对于一个运行不可信用户 cBot 的系统。

## 决策

每个远程主机运行一个独立的 `CtraderCliNode` **HTTP 代理**，**没有 SSH 和 shell**。主
应用通过 HTTP 调用代理；每个请求携带一个短期 **HS256 JWT**（5 分钟，
`iss=app-main` / `aud=app-node`），用该节点的密钥签名。代理：

- 仅运行匹配 `AllowedImagePrefix` 的镜像（带路径边界，所以 `ghcr.io/spotware` 不能
  匹配 `ghcr.io/spotware-evil/...`）；
- 通过 `ArgumentList` 执行 docker——永不是 shell 字符串；
- 是**无状态的**，通过 `app.instance` 标签找到容器；
- 自注册和心跳到 `POST /api/nodes/register`；主应用通过 `CtraderCliNode` upsert
  **按名称**，所以节点在 IP 变化中生存。

## 后果

- 泄漏的请求令牌在分钟内过期；没有站立 shell 凭证可被盗。
- 代理的能力限制为"运行允许的镜像"——它不能被转变为一般
  远程 shell。
- 节点身份是基于名称的，所以用新 IP 重新配置节点不会孤立其历史。
