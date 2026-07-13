---
slug: /contributing
title: Berkontribusi
description: Cara berkontribusi ke cMind — PR manusia atau berbantuan AI diterima. Kontribusi pertama dalam 10 menit.
sidebar_position: 5
---

# Berkontribusi ke cMind

Terima kasih sudah di sini. cMind menjadi lebih baik setiap kali seseorang membuka issue, melaporkan perilaku cTrader yang tepat, memperbaiki typo di dokumen ini, atau mengirim PR. **Anda tidak perlu menjadi wizard .NET** — tester, trader, dan doc-fixer sama berharganya dengan orang yang menulis agregat.

:::tip Panduan canonical hidup di repo
Halaman ini adalah friendly on-ramp. Proses penuh, selalu-terkini — aturan dasar, konvensi coding, alur review — ada di **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Kontribusi pertama Anda dalam ~10 menit

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 warning, atau CI akan politely menolak Anda
dotnet test           # unit + integration + E2E
```

Menemukan sesuatu untuk diperbaiki? Branch, ubah, tambahkan test, dan buka PR. Itulah seluruh loop.

## Cara membantu (tidak semuanya adalah kode)

| Kontribusi | Usaha | Di mana |
|---|---|---|
| Laporkan bug yang dapat direproduksi | 10 min | [Bug report](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| Sarankan fitur | 10 min | [Feature request](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| Tingkatkan dokumen ini | 15 min | Edit di bawah `website/docs/` dan PR |
| Tambahkan test yang hilang | 30 min | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| Laporkan perilaku cTrader yang tepat | 10 min | [Buka Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Aturan rumah (versi singkat)

cMind menangani **uang nyata**, jadi beberapa hal tidak dapat ditawar — dan jujur, itu membuat codebase menjadi kesenangan untuk dikerjakan:

- **Strict Domain-Driven Design.** Logika bisnis hidup pada agregat dan value object, tidak pernah di endpoint atau UI. (Ada playbook friendly untuk itu di repo.)
- **Tiga test tier, setiap perubahan.** Unit + integration + E2E, *termasuk* failure path (dropped connection, rejected order, dead node). Test hijau adalah harga masuk.
- **Zero warning.** `TreatWarningsAsErrors=true`. Idiom C# 14 modern.
- **Tidak ada secret, tidak ada magic string, tidak pernah `DateTime.UtcNow`** (inject `TimeProvider` sebagai gantinya).
- **Docs dalam commit yang sama.** Ubah behavior → update doc-nya. Ya, termasuk site ini.

Detail penuh, dengan *mengapa* di balik setiap aturan, di [CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) dan [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Berkontribusi dengan AI

Kami benar-benar menyambut **PR berbantuan AI** — proyek ini dibangun untuk dikerjakan oleh agent serta manusia. Jika Anda mengemudi Claude, Copilot, atau sejenisnya: arahkan ke [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), biarkan itu membaca file `CLAUDE.md` bersarang, dan pegang ke bar yang sama (test, zero warning, DDD). PR AI yang baik tidak dapat dibedakan dari PR manusia yang baik — review yang sama, sambutan yang sama.

## Jadilah hebat satu sama lain

Kami punya [Code of Conduct](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md). Intinya: bersikaplah baik, asumsikan itikad baik, dan ingat ada orang (atau agent orang) di ujung lain. Tanyakan pertanyaan lebih awal — itu kekuatan, bukan gangguan.

Selamat datang. Kami tidak sabar untuk melihat apa yang Anda bangun.
