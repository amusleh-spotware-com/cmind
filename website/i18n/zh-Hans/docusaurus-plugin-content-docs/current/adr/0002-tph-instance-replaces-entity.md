---
title: 0002 — 实例状态为 TPH；状态转换替换实体
description: 为什么实例的 ID 会在其生命周期中改变，以及为什么容器 ID 是稳定键。
---

# 0002 — 实例状态为 TPH；状态转换替换实体

## 背景

运行/回测实例会经历多个状态（pending → scheduled → starting → running → terminal）。
我们使用 EF Core **表-体系**（TPH）来建模状态：每个状态都是一个子类型
（`StartingRunInstance`、`RunningRunInstance` 等）。EF 的 TPH 判别器列**无法在**
现有行上更改。

## 决策

状态转换**用新的子类型实例替换实体**，而不是改变状态字段。因为行被替换，**实例 ID 会在** starting → running → terminal 转换期间改变。
**容器 ID 是稳定的**，并在转换中保持不变；HTTP 节点代理通过容器 ID 作为状态/报告/停止/日志的键进行处理。

## 后果

- 每个状态都是一个不同的类型，只拥有该状态有效的字段和方法 — 非法转换和不合理的字段访问是编译错误，而不是运行时检查。
- 调用者**不得**缓存跨转换的实例 ID；使用容器 ID 作为跨状态的稳定句柄。
- 转换逻辑存在于 `InstanceTransitions` 中；ID 变更是有意的，不是 bug。
