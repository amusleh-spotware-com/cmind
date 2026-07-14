---
title: Jalankannya secara tempatan
description: Dapatkan cMind berjalan di mesin anda sendiri dalam beberapa minit dengan Docker Compose (atau .NET Aspire untuk pembangunan).
sidebar_position: 1
---

# Jalankan cMind secara tempatan 🖥️

Ini adalah cara tercepat untuk melihat cMind sebenarnya — contoh penuh di mesin anda sendiri. Ambil kopi;
anda mungkin akan menyusun masuk sebelum itu sejuk.

:::tip[Apa yang anda akan ada pada akhirnya]
Aplikasi web yang berjalan di **localhost:8080**, pelayan MCP di **localhost:8081**, pangkalan data Postgres,
dan nod pekerja tempatan yang bersedia untuk membina dan menguji cBots belakang. Semuanya di mesin anda, semuanya milik anda.
:::

**Sebelum anda mula, anda memerlukan salah satu daripada:**

- **Hanya Docker** → gunakan Pilihan A (tiada .NET SDK diperlukan). Disyorkan untuk pandangan pertama.
- **.NET 10 SDK + Docker** → gunakan Pilihan B jika anda mahu mengganggu kod.

Kedua-dua jalan adalah lintas platform (Windows / macOS / Linux).

## Pilihan A — Docker Compose (tiada .NET SDK diperlukan)

Prasyarat: Docker Desktop (atau Docker Engine + plugin kompos).

```bash
cp .env.example .env        # edit PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- UI Web: <http://localhost:8080> (tandatangani dengan pemilik daripada `.env`; dipaksa mengubah kata laluan pada log masuk pertama).
- Pelayan MCP: <http://localhost:8081/mcp>.
- Data Postgres bertahan dalam volum `pgdata`; skema berhijrah secara automatik pada permulaan.

Bekas web memasang soket Docker hos (`/var/run/docker.sock`) jadi pembina dalam pelayar dan benih **LocalNode** binaan + calauan bekas cTrader Console pada mesin anda.

**Nota lintas platform**
- Docker Desktop (Windows/macOS) mendedahkan soket di `/var/run/docker.sock` — gunung kompos berfungsi seperti biasa.
- Linux: pastikan pengguna anda boleh mengakses soket, atau jalankan kompos dengan keistimewaan yang mencukupi.
- Imej web adalah `linux/amd64`; di Apple Silicon Docker menjalankannya di bawah emulasi.

Berhenti dan lap:

```bash
docker compose down          # simpan data
docker compose down -v       # juga padam volum pangkalan data
```

## Pilihan B — .NET Aspire (untuk pembangunan)

Prasyarat: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire mengorkestrasikan Postgres, Web, MCP, pgAdmin; wayar rentetan sambungan + OTLP; membuka papan pemuka. Tetapkan kredensial pemilik sebagai parameter Aspire (`OwnerEmail`, `OwnerPassword`).

Jalankan hanya aplikasi web terhadap Postgres sedia ada:

```bash
dotnet run --project src/Web
```

## Menambah nod pekerja secara tempatan

Nod Tempatan benih sudah menjalankan kerja pada mesin anda. Untuk menjalankan **auto-discovery** secara tempatan, mulakan ejen nod yang menunjuk kepada aplikasi Web (lihat [penemuan nod](../operations/node-discovery.md)) dengan `NodeAgent:MainUrl=http://host.docker.internal:8080` dan padankan `JoinToken`.

## Penyelesaian masalah 🔧

Docker mempunyai pendapat. Berikut adalah suspek biasa:

| Gejala | Kemungkinan sebab & pembaikan |
|---|---|
| `port sudah diperuntukkan` pada 8080/8081 | Sesuatu yang lain menggunakan port. Hentikannya, atau ubah pemetaan dalam `docker-compose.yml`. |
| Web bermula tetapi binaan/ujian belakang gagal | Soket Docker tidak dipasang atau mudah diakses. Di Linux, pastikan pengguna anda boleh mencapai `/var/run/docker.sock`. |
| `kebenaran ditolak` di soket (Linux) | Tambahkan pengguna anda ke kumpulan `docker` (`sudo usermod -aG docker $USER`) dan log masuk semula, atau jalankan dengan keistimewaan yang mencukupi. |
| Larian pertama yang sangat lambat | Pembinaan pertama menarik imej dan mengkompil — larian seterusnya jauh lebih cepat. Di Apple Silicon imej web `linux/amd64` berjalan di bawah emulasi. |
| Tidak boleh menyusun masuk | Periksa `OWNER_EMAIL` / `OWNER_PASSWORD` dalam `.env` anda. Log masuk pertama memaksa perubahan kata laluan. |
| Kebiasaan pangkalan data selepas naik taraf | `docker compose down -v` lap volum untuk papan tulis bersih (anda akan kehilangan data tempatan). |

Masih tersekat? [Buka Perbincangan](https://github.com/amusleh-spotware-com/cmind/discussions) — kami
mesra. Perhentian seterusnya: [sebarkan untuk nyata →](./cloud.md)
