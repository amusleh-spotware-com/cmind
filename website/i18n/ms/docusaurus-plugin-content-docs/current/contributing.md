---
slug: /contributing
title: Menyumbang
description: Bagaimana menyumbang kepada cMind — PR dibantu manusia atau AI diterima. Sumbangan pertama dalam 10 minit.
sidebar_position: 5
---

# Menyumbang kepada cMind 🛠️

Terima kasih kerana berada di sini. cMind menjadi lebih baik setiap kali seseorang membuka isu, melaporkan tingkah laku cTrader yang tepat,
membaiki kesalahan taip dalam dokumen ini sendiri, atau menghantar PR. **Anda tidak perlu menjadi ahli .NET**
— penguji, pedagang, dan pembaik dokumen dihargai sama seperti mereka yang menulis agregat.

:::tip[Panduan kanonik hidup dalam repo]
Halaman ini adalah lintasan mesra. Proses penuh, sentiasa terkini — peraturan asas, konvensyen pengkodean,
aliran ulasan — berada dalam **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Sumbangan pertama anda dalam ~10 minit

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 amaran, atau CI akan dengan sopan menolak anda
dotnet test           # unit + integrasi + E2E
```

Menemui sesuatu untuk diperbaiki? Cawang itu, ubah itu, tambah ujian, dan buka PR. Itulah keseluruhan gelung.

## Cara membantu (bukan semuanya kod)

| Sumbangan | Usaha | Di mana |
|---|---|---|
| 🐛 Laporkan pepijat yang boleh dihasilkan semula | 10 minit | [Laporan pepijat](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Cadangkan fitur | 10 minit | [Permintaan fitur](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Tingkatkan dokumen ini | 15 minit | Edit di bawah `website/docs/` dan PR |
| 🧪 Tambah ujian yang hilang | 30 minit | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Laporkan tingkah laku cTrader yang tepat | 10 minit | [Buka Perbincangan](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Peraturan rumah (versi ringkas)

cMind menggerakkan **wang sebenar**, jadi beberapa perkara tidak boleh ditawar — dan jujurnya, mereka menjadikan asas kod
kegembiraan untuk bekerja:

- **Reka Bentuk Berorientasikan Domain yang ketat.** Logik perniagaan hidup di agregat dan objek nilai, tidak pernah dalam
  titik akhir atau UI. (Ada playbook mesra untuk itu dalam repo.)
- **Tiga peringkat ujian, setiap perubahan.** Unit + integrasi + E2E, *termasuk* laluan kegagalan (sambungan terputus,
  perintah ditolak, nod mati). Ujian hijau adalah harga kemasukan.
- **Sifar amaran.** `TreatWarningsAsErrors=true`. Idiom C# 14 moden.
- **Tiada rahsia, tiada rentetan ajaib, tidak pernah `DateTime.UtcNow`** (suntik `TimeProvider` sebagai gantinya).
- **Dokumen dalam komit yang sama.** Ubah tingkah laku → kemaskini doknya. Ya, itu termasuk laman ini.

Butiran penuh, dengan *sebab* di sebalik setiap peraturan, dalam
[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) dan
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Menyumbang dengan AI 🤖

Kami dengan tulus menyambut **PR yang dibantu AI** — projek ini dibina untuk diusahakan oleh ejen serta
manusia. Jika anda memandu Claude, Copilot, atau serupa: arahkannya kepada
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), biarkan ia membaca fail
`CLAUDE.md` bersarang, dan pegang ia ke bar yang sama (ujian, sifar amaran, DDD). PR AI yang baik adalah
tidak dapat dibezakan daripada PR manusia yang baik — ulasan yang sama, sambutan yang sama.

## Jadilah cemerlang antara satu sama lain

Kami mempunyai [Kod Kelakuan](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md).
Esensi: jadilah baik, andaikan itikad baik, dan ingat ada seseorang (atau ejen seseorang) di
hujung yang lain. Tanya soalan awal — itu adalah kekuatan, bukan gangguan.

Selamat datang di atas kapal. Kami tidak sabar untuk melihat apa yang anda bina. 🎉
