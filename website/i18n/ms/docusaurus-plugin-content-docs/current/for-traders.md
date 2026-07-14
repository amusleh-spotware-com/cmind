---
slug: /for-traders
title: cMind untuk pedagang cTrader
description: Mengapa pedagang cTrader perlu hos-sendiri cMind — miliki tindanan dan data anda, pengarang, ujian belakang, jalankan dan pantau cBots dalam satu konsol berkuasa AI, di laptop, VPS atau telefon anda.
keywords:
  - cTrader
  - Perdagangan algoritma
  - Platform perdagangan yang dihoskan sendiri
  - Ujian belakang cBot
  - Bot perdagangan AI
  - Perisian perdagangan sumber terbuka
sidebar_position: 5
---

# cMind untuk pedagang cTrader 📈

Anda sudah berdagang di cTrader. Anda sudah menggugut editor kod, backtester, VPS, dan tiga
tab pelayar. **cMind meruntuhkan semua itu menjadi satu konsol gelap, mesra papan kunci yang anda jalankan
sendiri** — dan ia adalah sumber terbuka, jadi tidak ada apa-apa tentang tepi anda, strategi anda, atau kredensial anda
pernah meninggalkan kotak anda.

:::tip[TL;DR]
Hos-sendiri cMind di laptop, VPS murah, atau pelayan rumah. Pengarang, ujian belakang, jalankan, dan pantau cBots
di satu tempat, dengan inti AI melakukan kerja-kerja. → [Jalankannya dalam 5 minit](./deployment/local.md)
:::

## Mengapa hos-sendiri bukannya perkhidmatan yang disempurnakan?

- **Miliki tindanan dan data anda.** cBots anda, kredensial, token, dan sejarah ekuiti anda hidup di
  infrastruktur **anda** — tiada pihak ketiga, tiada kunci, tiada e-mel "kami matahari menetapkan produk ini".
- **Ia benar-benar milik anda untuk berubah.** C# 14 / .NET 10, DDD ketat, EF Core + PostgreSQL, pelayan MCP
  — semuanya sumber terbuka dan boleh digodam. Forkit, panjangkannya, hantar PR.
- **Tiada paywall setiap fitur.** Bawa kunci AI anda sendiri untuk mana-mana penyedia; setiap fitur AI adalah hidup.

Lebih suka tidak menjalankan pelayan sendiri? Syarikat pengehosan boleh menjalankan cMind yang terurus untuk anda —
lihat [Untuk penyedia cloud & VPS](./for-cloud-providers.md).

## Satu konsol, tanpa gugut tab

- **Pengarang** dalam IDE Monaco sebenar (editor VS Code), dengan templat C# **dan** Python dan
  binaan `dotnet` terpencil dalam bekas yang boleh dilupakan. → [Binaan & ujian belakang](./features/build-and-backtest.md)
- **Ujian belakang** merentasi armada nod dan tonton lengkung ekuiti mengalir balik hidup.
- **Jalankan** strategi hidup dan **pantau** mereka daripada satu papan pemuka. → [Papan pemuka](./features/dashboard.md)
- **Salinan** akaun induk ke banyak akaun merentasi broker dan ID cTrader, dengan penyerasian
  yang bertahan sambungan terputus dan token berputar. → [Salinan perdagangan](./features/copy-trading.md)

## AI yang melakukan kerja-kerja, bukan percakapan kecil

Bawa kunci API anda sendiri (mana-mana penyedia yang disokong — cloud atau model tempatan) dan dapatkan kebiasaan Inggeris → sebuah
cBot yang benar-benar dikompil dengan gelung perbaikan diri, penalaan parameter, autopsi ujian belakang, dan risiko
pengawal yang boleh berhenti automatik bot yang misbehave. → [Bertemu inti AI](./features/ai.md)

## Alatan gred institusi, untuk satu

Ketat yang meja bayar, di kotak anda sendiri:

- [Integriti ujian belakang](./features/backtest-integrity.md) · [Saiz kedudukan](./features/position-sizing.md)
- [Kesihatan strategi](./features/strategy-health.md) · [Makmal rejim](./features/regime-lab.md)
- [TCA pelaksanaan](./features/execution-tca.md) · [Jurnal perdagangan](./features/trading-journal.md)
- [Studio ejen](./features/agent-studio.md) · [Kedudukan kontrarian](./features/contrarian-positioning.md)

## Berjalan di mana anda berada

Mulai di laptop anda dengan `docker compose up`, naik taraf ke VPS murah atau pelayan rumah apabila anda
bersedia, dan periksa bot anda dari telefon anda — cMind adalah [PWA](./features/pwa.md) yang boleh dipasang, mudah alih pertama. → [Jalankannya secara tempatan](./deployment/local.md)

Mahu klien AI anda menggunakannya? Ada [pelayan MCP](./features/mcp.md) terbina dalam.

## Bantu buatnya lebih baik

cMind adalah sumber terbuka dan MIT-berlesen — peta jalan dibentuk komuniti:

- Fail isu dan permintaan fitur, dan undi apa yang penting.
- Tambah templat cBot, penyesuai penyedia AI, atau terjemahan UI.
- Hantar PR — tiga peringkat ujian (unit + integrasi + E2E) dan DDD ketat menjaga bar tinggi, dan
  [Panduan Menyumbang](./contributing.md) berjalan anda melaluinya.

Bersedia? → [Baca pengenalan](./intro.md) kemudian [jalankannya secara tempatan](./deployment/local.md).
