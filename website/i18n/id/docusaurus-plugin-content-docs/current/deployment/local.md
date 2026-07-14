---
title: Jalankan secara lokal
description: Dapatkan cMind berjalan di mesin Anda sendiri dalam beberapa menit dengan Docker Compose (atau .NET Aspire untuk development).
sidebar_position: 1
---

# Jalankan cMind secara lokal

Ini adalah cara tercepat untuk melihat cMind secara nyata — instance penuh di mesin Anda sendiri. Ambil kopi; Anda mungkin akan sign in sebelum dingin.

:::tip[Apa yang akan Anda miliki di akhir]
Aplikasi web yang berjalan di **localhost:8080**, server MCP di **localhost:8081**, database Postgres, dan local worker node siap untuk build dan backtest cBot. Semua di mesin Anda, semua milik Anda.
:::

**Sebelum Anda mulai, Anda butuh satu dari:**

- **Hanya Docker** → gunakan Option A (tidak memerlukan .NET SDK). Recommended untuk first look.
- **.NET 10 SDK + Docker** → gunakan Option B jika Anda ingin hack kode.

Kedua path adalah cross-platform (Windows / macOS / Linux).

## Option A — Docker Compose (tanpa .NET SDK diperlukan)

Prereq: Docker Desktop (atau Docker Engine + compose plugin).

```bash
cp .env.example .env        # edit PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Web UI: <http://localhost:8080> (sign in dengan owner dari `.env`; dipaksa untuk ubah password pada first login).
- Server MCP: <http://localhost:8081/mcp>.
- Data Postgres persist dalam volume `pgdata`; schema migrate otomatis pada startup.

Web container mount host Docker socket (`/var/run/docker.sock`) sehingga in-browser builder dan seeded **LocalNode** build + run container cTrader Console di mesin Anda.

**Catatan cross-platform**
- Docker Desktop (Windows/macOS) expose socket di `/var/run/docker.sock` — compose mount bekerja apa adanya.
- Linux: pastikan user Anda dapat akses socket, atau jalankan compose dengan privilege cukup.
- Image Web adalah `linux/amd64`; di Apple Silicon Docker menjalankannya di bawah emulation.

Stop dan wipe:

```bash
docker compose down          # simpan data
docker compose down -v       # juga hapus volume database
```

## Option B — .NET Aspire (untuk development)

Prereq: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire mengorkestra Postgres, Web, MCP, pgAdmin; wire connection string + OTLP; buka dashboard. Set owner kredensial sebagai Aspire parameter (`OwnerEmail`, `OwnerPassword`).

Jalankan hanya app web terhadap Postgres yang ada:

```bash
dotnet run --project src/Web
```

## Tambahkan worker node secara lokal

Seeded LocalNode sudah menjalankan work di mesin Anda. Untuk exercise **auto-discovery** secara lokal, mulai node agent menunjuk ke Web app (lihat [node discovery](../operations/node-discovery.md)) dengan `NodeAgent:MainUrl=http://host.docker.internal:8080` dan cocok `JoinToken`.

## Troubleshooting

Docker punya pendapat. Di sini suspect biasa:

| Symptom | Kemungkinan penyebab & fix |
|---|---|
| `port is already allocated` pada 8080/8081 | Sesuatu yang lain menggunakan port. Stop, atau ubah mapping di `docker-compose.yml`. |
| Web start tetapi build/backtest gagal | Docker socket tidak di-mount atau accessible. Di Linux, pastikan user Anda dapat reach `/var/run/docker.sock`. |
| `permission denied` di socket (Linux) | Tambahkan user Anda ke `docker` group (`sudo usermod -aG docker $USER`) dan re-login, atau jalankan dengan privilege cukup. |
| Pertama run sangat lambat | Build pertama pull image dan compile — run berikutnya jauh lebih cepat. Di Apple Silicon image web `linux/amd64` berjalan di bawah emulation. |
| Tidak dapat sign in | Check `OWNER_EMAIL` / `OWNER_PASSWORD` di `.env` Anda. First login memaksa password change. |
| Weirdness database setelah upgrade | `docker compose down -v` wipe volume untuk clean slate (Anda akan kehilangan data lokal). |

Masih stuck? [Buka Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) — kami friendly. Next stop: [deploy untuk nyata →](./cloud.md)
