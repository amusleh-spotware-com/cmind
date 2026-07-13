---
title: 0006 — Penghosan salin diselaraskan oleh pajakan DB atom
description: Mengapa profil salin dituntut melalui pajakan Postgres atom daripada koordinator berdedikasi, dan bagaimana itu mencegah penyalinan berganda.
---

# 0006 — Penghosan salin diselaraskan oleh pajakan DB atom

## Konteks

Profil salin yang berjalan mestilah dihoskan oleh **tepat satu** nod — dua hos pada profil yang sama bermakna setiap perdagangan sumber dicerminkan dua kali (wang sebenar hilang). Nod datang dan pergi (penskalaan, kemalangan, pembaruan bergulir), dan kami tidak mahu perkhidmatan koordinator yang terpisah untuk berjalan dan kekal hidup.

## Keputusan

Setiap `CopyEngineSupervisor` menuntut profil dengan **pajakan DB atom** pada meja `CopyProfiles`:

- **Tuntutan** — `ExecuteUpdate` atom (atau `FOR UPDATE SKIP LOCKED` apabila had per nod) mengambil profil yang tidak ditugaskan *atau* yang pajakan telah lapor. Atomisitas bermakna dua penyelia perlumbaan tidak pernah menuntut baris yang sama.
- **Perbaharui** — nod langsung menyegarkan pajakan setiap kitaran, jadi ia memastikan tuntutannya.
- **Rampas** — pajakan nod yang jatuh tamat, dan penyelamat mengambil profil pada kitaran seterusnya (penyembuhan diri). Pada penutupan anggun nod **melepaskan** pajakannya dengan segera jadi failover cepat.
- **Pengawal anjing** — hos yang tugasnya telah keluar semasa profil masih milik kami dimulai semula.
- Suai dijitter untuk mengelakkan kumpulan petir UPDATE di skala.

## Akibat

- Tiada koordinator kendiri untuk disebarkan atau kekal sihat — Postgres ialah sumber kebenaran tunggal.
- Penyalinan berganda dicegah oleh atomisitas peringkat baris, bukan oleh kunci peringkat aplikasi.
- Kependaman failover terbatas oleh TTL pajakan (tolak laluan cepat keluaran anggun).
- Ini ialah laluan wang; ia dijaga oleh rangkaian tekanan deterministik (DST) — tidak pernah lemahkan skenario DST membuatnya lalu.
