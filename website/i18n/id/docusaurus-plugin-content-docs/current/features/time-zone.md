---
description: "Setiap waktu yang ditampilkan muncul dalam zona waktu Anda sendiri — dideteksi dari browser pada kunjungan pertama dan dapat diubah dari Pengaturan. Penyimpanan dan API tetap UTC."
---

# Zona waktu

Setiap waktu yang ditampilkan aplikasi ditampilkan dalam zona waktu Anda sendiri, bukan server. Pilihan Anda disimpan ke profil dan mengikuti Anda di seluruh perangkat.

Pada kunjungan pertama aplikasi otomatis mengadopsi zona browser Anda. Anda dapat mengubahnya kapan saja di Pengaturan → Zona waktu; default penerapan adalah opsi white-label App:Branding:DefaultTimeZone (default UTC). Waktu selalu disimpan dan dikembalikan oleh API dalam UTC — hanya tampilan yang dikonversi.

- Urutan resolusi: zona profil, lalu cookie, lalu default penerapan, lalu UTC.
- Deteksi berjalan sekali dan tidak pernah menimpa zona yang Anda pilih.
- Format mengikuti bahasa Anda; label relatif seperti «2 menit lalu» tidak terpengaruh.
