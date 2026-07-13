---
id: white-label-owner-settings
title: Pilihan white-label dalam tetapan Pemilik
sidebar_label: Tetapan pemilik white-label
---

# Pilihan white-label dalam Tetapan Pemilik

Setiap pilihan white-label yang boleh ditetapkan oleh penempatan melalui konfigurasi (`appsettings`/env) **juga boleh**
ditetapkan pada masa jalan oleh pemilik apl, dari **Tetapan → Penempatan**, tanpa penempatan semula. Utama tuggakan **mengalahkan konfigurasi**; membersihkannya mengembalikan pilihan kepada nilai yang dikonfigurasi (atau lalai terbina dalam) penempatan.

Ini mencerminkan cara penempatan *white-label* mengkonfigur produk — knob yang sama, kesan yang sama —
jadi operador boleh menala branding, gerbang dan dasar secara langsung dan lihat hasilnya serta-merta.

## Di mana nó tinggal

- **UI:** bahagian **Penempatan** eksklusif dalam dialog tetapan, dan halaman deep-linkable **`/settings/deployment`**. Pilihan dikumpulkan ke dalam **tab setiap kategori** (Branding, Tema,
  Ciri, Pendaftaran, Akaun, E-mel, AI, Open API, Prop firm), mudah alih-pertama, dengan dialog berjendela
  pada desktop dan permukaan skrin penuh pada telefon.
- **API:** `/api/whitelabel` (pemilik sahaja, tidak pernah gerbang ciri):
  - `GET /api/whitelabel` — setiap pilihan dengan nilai berkesan, sumber (`Config` / `Owner` /
    `Default`) dan sama ada tuggakan ditetapkan. **Rahsia disamarkan** (nilai tidak pernah dikembalikan).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — tetapkan tuggakan (disahkan setiap jenis pilihan). Nilai kosong pada pilihan **rahsia** menyimpan rahsia sedia ada.
  - `DELETE /api/whitelabel/{key}` — jelaskan satu tuggakan (kembali ke konfigurasi).
  - `POST /api/whitelabel/reset` — jelaskan **semua** tuggakan (kembali penempatan kepada konfigurasi murni).

## Cara tuggakan berkuatkuasa

Tuggakan pemilik disimpan sebagai baris `AppSetting` yang dienkripsi-di mana-perlu dan dilapis di atas `AppOptions`
yang diikat oleh `IOptionsMonitor<AppOptions>` yang dihiasi. Kerana setiap pengguna sudah membaca pilihan
melalui monitor itu, tuggakan применяется **secara langsung merentasi seluruh apl** — tema, tajuk halaman, gerbang MFA,
gerbang pembekal AI, senaraibenarkan broker, dasar pendaftaran, tetapan pengangkutan e-mel, dll. kemas kini
pada baca seterusnya (tema/branding diproses semula serta-merta). Jika pangkalan data tidak tersedia sebentar, lapisan **gagal dibuka** kepada paksi konfigurasi, jadi pembacaan tuggakan tidak boleh merosakkan apl.

**Togol ciri** adalah sebahagian daripada permukaan yang sama tetapi dikekalkan melalui kedai tuggakan sedia ada
(`IFeatureGate`), jadi tab Ciri dan togolan ciri solo tidak pernah berselisih.

**Rahsia** (kata lalu SMTP, rahsia CAPTCHA, rahsia peruntukan) dienkripsi pada rehat
(`ISecretProtector`, tujuan `whitelabel.secret`), tulis sahaja dalam UI, dan tidak pernah dikembalikan oleh API.

## Pilihan yang didelegasikan

**Kredensi aplikasi Open API kongsi** dan **selimit kadar setiap jenis pesan** diurus di bahagian tetapan Open API (lihat docs salinan-perdagangan / Open API). nó muncul dalam katalog Penempatan sebagai entri *didelegeasikan* (baca sahaja di sini, dengan pautan) jadi tiada penduaan dan jaminan sync masih mengira nó sebagai diliputi.

## Sentiasa sepadan (dikuatkuasakan)

Menambah pilihan white-label baharu kepada konfigurasi **mesti** memaparkannya dalam tetapan pemilik dalam komit yang sama. Ini dikuatkuasakan oleh `WhiteLabelCatalogParityTests`: nó memantul atas setiap sifat rekod pilihan white-label dan gagal bina melainkan sifat itu berdaftar dalam
`Core/WhiteLabel/WhiteLabelCatalog` (atau disenaraikan secara eksplisit dalam `IntentionallyExcluded` dengan sebab).
Lihat mandat 10 dalam `CLAUDE.md`.

## Nota

- Mengaktifkan SMTP pada penempatan yang bermula dengan **tiada** e-mel dikonfigurasi memerlukan重启 (jenis pengirim dipilih pada permulaan); hos/kredensi pengirim yang sudah dikonfigurasi dikemas kini secara langsung.
- **Label/penerangan pilihan** ialah ID teknikal knob konfigurasi yang ditunjuk sebagai data; label tab dan semua chroma interaktif sepenuhnya disesuaikan.
