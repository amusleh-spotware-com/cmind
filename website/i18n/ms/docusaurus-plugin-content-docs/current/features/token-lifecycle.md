---
description: "Open API cTrader membenarkan satu token akses sah setiap ID cTrader (cID) pada satu masa. Pada saat token baharu dikeluarkan — penyegaran berjadual, atau pengesahan semula apabila pengguna memautkan akaun lain pada cID yang sama — token akses sebelumnya dibatalkan."
---

# Kitaran hayat token Open API

Open API cTrader membenarkan **satu token akses sah setiap ID cTrader (cID) pada satu masa**. Pada saat
token baharu dikeluarkan — penyegaran berjadual, atau pengesahan semula apabila pengguna memautkan akaun lain
pada cID yang sama — token akses sebelumnya dibatalkan. Enjin salinan yang berjalan pada nod jarak jauh sedang memegang token yang sudah mati itu, jadi token baharu harus sampai tanpa memutuskan
sambungan langsung.

## Model

- **`OpenApiAuthorization`** ialah agregat yang memegang token akses + penyegaran tersulit untuk satu cID. Indeks unik pada `(UserId, CtidUserId)` menguatkuasakan **tepat satu kebenaran setiap cID
  setiap pengguna**.
- **`TokenVersion`** — pembilang monotonic yang dinaikkan setiap kali token berputar (`Refresh()`,
  yang juga merangkumi laluan pengesahan semula apabila akaun lain dipautkan pada cID yang sama). Ia ialah
  penanda versi untuk peraturan token-sah-tunggal dan apa yang digunakan oleh hos yang berjalan untuk mengesan
  perubahan walaupun dua rentetan token berlaku bertembung.
- Token disulit pada rehat melalui `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`). Mereka tidak pernah dicatat atau disimpan dalam teks jelas.

## Perambatan (pertukaran di tempat yang aman)

1. Token berputar → token baharu + `TokenVersion` yang dinaikkan dikekalkan.
2. `CopyEngineSupervisor` pada nod hos membaca semula pelan setiap kitaran sepadan dan
   mengira **tandatangan token** (token akses + versi). Perubahan bermakna putaran.
3. Berbanding merobohkan hos dan memulakan semula (yang akan memutuskan aliran pelaksanaan master),
   penyelia ** menolak token baharu ke hos yang berjalan**.
4. Hos mengesahkan semula akaun yang terjejas **pada soket sedia ada**
   (`ProtoOAAccountAuthReq` lagi) melalui `SwapAccessTokenAsync`, kemudian melakukan sepadanan ringan. Token
   lama mati; strim salinan tidak pernah berhenti.

Inilah yang menjadikan kes lintas-cID selamat: pengguna menambah akaun kedua dari cID yang sama
pertengahan-lari membatalkan token lama, dan profil salinan yang berjalan terus pada yang baharu.

## Penyegaran

`OpenApiTokenRefreshService` (latar belakang) menyegarkan kebenaran sebelum tamat tempoh secara proaktif;
`OpenApiAuthorization.IsExpiring(threshold, now)` mengawalinya. cTrader memutar **refresh** token
pada setiap penyegaran, jadi token penyegaran baharu dikekalkan serta-merta; cache baca-sahaja yang tidak boleh
mengekalkan akan membatalkan dirinya sendiri (berkaitan dengan kerja ujian dalam-kluster, yang memasang salinan boleh tulis
dari rahsia).

### Pen escalaan kegagalan

Penyegaran yang gagal tidak senyap. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
merekam `RefreshFailedAt`, menambah `ConsecutiveRefreshFailures`, dan sentiasa membangkitkan
`AccessTokenRefreshFailed` (amaran). Apabila token berada dalam `App:OpenApi:TokenRefreshCriticalWindow`
(lalai 6j) dari tamat tempoh dan penyegaran masih gagal, ia meningkat **sekali** dengan
peristiwa domain `AccessTokenRefreshCritical` + `Critical` log supaya pemilik boleh mengesahkan semula sebelum
operasi salinan/prop-firm kehilangan token. Pembilang kegagalan dan latch escalaan menetapkan semula pada `Refresh` yang berjaya seterusnya. Perkhidmatan terus mencuba setiap `TokenRefreshInterval`, jadi gangguan pembekal/penyelenggaraan disembuhkan sendiri apabila titik akhir penyegaran kembali.

## Amaran pembatalan & pemulihan automatik (M1)

Pengesahan semula/sekali lagi pada cID membatalkan token yang masih dipegang oleh hos salinan yang berjalan. Apabila panggilan perdagangan menolak dengan `OpenApiErrorKind.TokenInvalid`, hos membangkitkan amaran **`CopyTokenInvalidated`** yang berbeza (log 1078) — bukan kegagalan generik — supaya saluran pemberitahuan tahu token memerlukan perhatian. Pemulihan automatik: penyelia membaca semula kebenaran setiap kitaran dan,
apabila token yang disegarkan menukar tandatangan token, mendorongnya ke hos yang berjalan untuk **pertukaran di tempat** — salinan disambung semula tanpa perlu masukkan semula secara manual. Profil `NotLinkable` (token/auth buat sementara tidak boleh diselesaikan) juga dinilai semula setiap kitaran penyelia dan dihos sebaik sahaja pelan dibina semula.

## Pengawal hayat hos (M2)

Penyelia mengesan setiap tugas hos profil yang dihos. Jika hos keluar atau rosak semasa profilnya masih diperuntukkan kepada nod ini, pengawal membatalkan dan **memulakan semula** ia pada kitaran seterusnya (log
`CopyHostRestarted`), jadi hos yang tersumbat disembuhkan sendiri berbanding memerlukan pemulaian semula manual — dan kegagalan satu profil tidak pernah melambatkan yang lain (pengasingan setiap profil).

## Ujian

- **Unit** — `TokenVersion` naik pada `Refresh`; hos melakukan pertukaran di tempat tanpa memulakan semula;
  pembatalan lintas-cID menukar token sumber dan destinasi; **token destinasi yang dibatalkan membangkitkan
  `CopyTokenInvalidated` dan dipulihkan secara automatik pada tolakan token seterusnya** (M1); keputusan `IsHostDead` pengawal semula hos yang lengkap/rosak dan meninggalkan profil yang diperuntukkan sendiri (M2).
- **Integrasi** — `TokenVersion` bertekak + meningkat melalui EF pada Postgres sebenar; tandatangan token berubah pada kenaikan versi token walaupun rentetan tidak berubah.
