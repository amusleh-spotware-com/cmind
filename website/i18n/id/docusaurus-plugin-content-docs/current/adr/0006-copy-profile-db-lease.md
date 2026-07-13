---
title: 0006 — Hosting copy dikoordinasikan oleh lease DB atomik
description: Mengapa profil copy diklaim melalui lease Postgres atomik daripada coordinator khusus, dan bagaimana itu mencegah double-copying.
---

# 0006 — Hosting copy dikoordinasikan oleh lease DB atomik

## Konteks

Profil copy yang berjalan harus di-host oleh **exactly one** node — dua host pada profil yang sama berarti setiap source trade dicerminkan dua kali (uang nyata hilang). Node datang dan pergi (scaling, crash, rolling update), dan kami tidak ingin layanan coordinator terpisah berjalan dan tetap hidup.

## Keputusan

Setiap `CopyEngineSupervisor` mengklaim profil dengan **lease DB atomik** pada tabel `CopyProfiles`:

- **Claim** — sebuah atomic `ExecuteUpdate` (atau `FOR UPDATE SKIP LOCKED` saat capping per-node) mengambil profil yang tidak ditugaskan *atau* yang lease-nya telah berlalu. Atomicity berarti dua supervisor yang racing tidak pernah mengklaim row yang sama.
- **Renew** — node hidup menyegarkan lease-nya setiap cycle, sehingga tetap mempertahankan klaim-nya.
- **Reclaim** — lease node yang crash kadaluarsa, dan survivor mengambil profil di cycle berikutnya (self-heal). Pada shutdown graceful node **releases** lease-nya segera sehingga failover cepat.
- **Watchdog** — host yang task-nya telah keluar sementara profil masih milik kami di-restart.
- Reconcile di-jitter untuk menghindari thundering herd dari `UPDATE` dalam skala besar.

## Konsekuensi

- Tidak ada coordinator standalone untuk deploy atau tetap sehat — Postgres adalah single source of truth.
- Double-copying dicegah oleh atomicity row-level, bukan locking application-level.
- Failover latency dibatasi oleh lease TTL (minus fast-path graceful release).
- Ini adalah money path; itu dijaga oleh deterministic stress suite (DST) — jangan pernah melemahkan scenario DST untuk membuat itu lolos.
