---
title: Triển khai lên cloud
description: Cách triển khai cMind lên Docker, Kubernetes, Azure, AWS hoặc các cloud khác — một lộ trình với các ví dụ từng bước.
sidebar_position: 2
---

# Triển khai lên cloud

cMind là một ứng dụng .NET Aspire, được đóng gói dưới dạng các vùng chứa Docker. Triển khai có ba tầng:

| Tầng | Giải pháp |
|---|---|
| **Stateless** (Web + MCP) | Bất kỳ container runtime, tự động chia tỷ lệ |
| **Database** | PostgreSQL được quản lý (hoặc tự lưu trữ) |
| **Node fleet** | Kubernetes, VM hoặc EC2 (cần Docker để chạy cBot) |

Hãy xem hướng dẫn theo nhà cung cấp của bạn.
