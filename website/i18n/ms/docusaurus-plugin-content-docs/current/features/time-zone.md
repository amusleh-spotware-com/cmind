---
description: "Setiap masa yang dipaparkan muncul dalam zon waktu anda sendiri — dikesan daripada pelayar pada lawatan pertama dan boleh ditukar daripada Tetapan. Storan dan API kekal UTC."
---

# Zon waktu

Setiap masa yang dipaparkan apl ditunjukkan dalam zon waktu anda sendiri, bukan pelayan. Pilihan anda disimpan ke profil dan mengikuti anda merentas peranti.

Pada lawatan pertama apl mengambil zon pelayar anda secara automatik. Anda boleh menukarnya bila-bila masa di Tetapan → Zon waktu; lalai penggunaan ialah pilihan white-label App:Branding:DefaultTimeZone (lalai UTC). Masa sentiasa disimpan dan dikembalikan oleh API dalam UTC — hanya paparan ditukar.

- Susunan resolusi: zon profil, kemudian kuki, kemudian lalai penggunaan, kemudian UTC.
- Pengesanan berjalan sekali dan tidak pernah menimpa zon yang anda pilih.
- Pemformatan mengikut bahasa anda; label relatif seperti «2 minit lalu» tidak terjejas.
