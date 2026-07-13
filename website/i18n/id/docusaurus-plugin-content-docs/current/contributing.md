---
slug: /contributing
title: Berkontribusi
description: Cara berkontribusi ke cMind — PR manual atau berbantuan AI diterima. Kontribusi pertama dalam 10 menit.
sidebar_position: 5
---

# Berkontribusi ke cMind 🛠️

Terima kasih telah berada di sini. cMind menjadi lebih baik setiap kali seseorang membuka issue, melaporkan perilaku cTrader yang tepat, memperbaiki typo di docs ini, atau mengirimkan PR. **Anda tidak perlu menjadi wizard .NET** — tester, trader, dan doc-fixer sama berharganya dengan orang yang menulis agregat.

:::tip Panduan kanonik hidup di repo
Halaman ini adalah on-ramp yang ramah. Proses penuh, selalu-terkini — aturan dasar, konvensi coding,
aliran review — ada di **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Kontribusi pertama Anda dalam ~10 menit

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 warnings, atau CI akan dengan sopan menolak Anda
dotnet test           # unit + integration + E2E
```

Menemukan sesuatu yang diperbaiki? Branch itu, ubah, tambahkan tes, dan buka PR. Itu seluruh loop.

## Cara membantu (tidak semuanya adalah kode)

| Kontribusi | Usaha | Di mana |
|---|---|---|
| 🐛 Laporkan bug yang dapat direproduksi | 10 menit | [Bug report](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Sarankan fitur | 10 menit | [Feature request](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Tingkatkan docs ini | 15 menit | Edit di bawah `website/docs/` dan PR |
| 🧪 Tambahkan tes yang hilang | 30 menit | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Laporkan perilaku cTrader yang tepat | 10 menit | [Buka Diskusi](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Aturan rumah (versi pendek)

cMind memindahkan **uang nyata**, jadi beberapa hal tidak dapat dinegosiasikan — dan jujur, mereka membuat codebase
menjadi kesenangan untuk dikerjakan:

- **Domain-Driven Design yang ketat.** Logika bisnis hidup di agregat dan value object, tidak pernah di
  endpoint atau UI. (Ada playbook yang ramah untuk itu di repo.)
- **Tiga tingkat tes, setiap perubahan.** Unit + integration + E2E, *termasuk* jalur kegagalan (koneksi dropped,
  order ditolak, node mati). Tes hijau adalah harga penerimaan.
- **Nol peringatan.** `TreatWarningsAsErrors=true`. Idiom C# 14 modern.
- **Tidak ada secrets, tidak ada magic strings, tidak pernah `DateTime.UtcNow`** (injeksi `TimeProvider` sebaliknya).
- **Docs dalam commit yang sama.** Ubah perilaku → perbarui docs-nya. Ya, itu termasuk situs ini.

Detail lengkap, dengan *mengapa* di balik setiap aturan, dalam
[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) dan
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Berkontribusi dengan AI 🤖

Kami benar-benar menyambut **PR berbantuan AI** — proyek ini dibangun untuk dikerjakan oleh agen serta manusia. Jika Anda menjalankan Claude, Copilot, atau serupa: arahkan ke [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), biarkan baca
file `CLAUDE.md` bersarang, dan tahan ke standar yang sama (tes, nol peringatan, DDD). PR AI yang baik tidak bisa dibedakan dari PR manusia yang baik — review yang sama, selamat yang sama.

## Jadilah excellent satu sama lain

Kami memiliki [Code of Conduct](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md).
Intinya: bersikap baik, asumsikan good faith, dan ingat ada orang (atau agen orang) di
ujung lain. Tanyakan pertanyaan lebih awal — itu adalah kekuatan, bukan gangguan.

Selamat datang di papan. Kami tidak sabar untuk melihat apa yang Anda bangun. 🎉
