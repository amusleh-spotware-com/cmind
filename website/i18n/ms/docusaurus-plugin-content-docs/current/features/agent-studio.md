---
description: "Agent Studio — cipta ejen perdagangan berwatak, tanpa kod dengan aksara dan archetyped yang urus akaun ke arah matlamat anda di bawah Autonomy & Safety Kernel (sampul risiko, pemutus litar, suis bunuh, persetujuan penafian berversikan)."
---

# Agent Studio

Agent Studio membolehkan anda cipta **ejen perdagangan dengan personaliti** — tanpa kod — dan berikan nó pengurusan
akaun anda ke arah matlamat yang boleh diukur. Ejen adalah seperti cBot berpersonaliti: anda pilih archetyped
dan sikap, tetapkan pengawal keselamatan, dan nó berjalan di bawah **Autonomy & Safety Kernel**.

Buka **AI → Agent Studio** (`/agent-studio`).

## Cipta ejen

Dialog **Ejen baharu** mengumpul, tanpa kod:

- **Nama** dan **archetype** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion atau Breakout/Momentum. Setiap preset memperbaiki irama dan postur yang masuk akal.
- **Sikap** — slider agresiviti, kesabaran dan trend-following.
- **Akaun yang diurus** — **sekurang-kurangnya satu diperlukan untuk cipta ejen** (ejen tanpa akaun tidak pernah boleh bermula, jadi *Cipta* tetap dilumpuhkan sehingga anda pilih satu). Jika anda belum pautkan akaun perdagangan lagi, dialog berkata demikian dan arahkan anda untuk pautkan satu dahulu.
- **Tahap autonomi** — **Nasihat** (cadangkan sahaja) atau **Kelulusan-digerbang** (bertindak hanya selepas kelulusan setiap tindakan). **Auto Penuh** (tiada kelulusan setiap dagangan) tambahan memerlukan **sampul risiko**
  dan penerimaan penafian risiko sebelum boleh mengaktifkan.

Personaliti mengkompil **secara deterministik** menjadi prompt sistem ejen (tiada LLM menulis nó), jadi konfigurasi yang sama
sentiasa menghasilkan Arahan yang sama — reproduisible dan boleh审计.

## Barisan kawalan

Setiap ejen dipaparkan dalam jadual bilik kawalan: **ejen mana, jenisnya, berapa banyak akaun ia urus,
matlamat, status lari, dan tindakan terakhir**, dengan kawalan **Mula / Henti / Bunuh**. Suis Bunuh memberhentikan ejen yang berjalan serta-merta.

## Keselamatan ialah invariant domain, bukan tetapan

Semua yang menyentuh wang mengalir melalui **Autonomy & Safety Kernel**:

- **Sampul risiko** — had tegas setiap pesanan (harian rugi max, pendedahan terbuka, saiz kedudukan, leverage,
  kerugian berturut-turut, pesanan/jam, simbol dibenarkan). Setiap pesanan disahkan melaluinya sebelum dihantar;
  pelanggaran ditolak, bukan di clamp. Diperlukan sebelum ejen boleh sampai ke Auto Penuh.
- **Pemutus litar** — diberhentikan secara deterministik pada rentetan rugi, pelanggaran harian-rugi, **pelanggaran matlamat prestasi susah**, atau **ketidaksediaaan pembekal AI** (model mati atau halusinasi tidak membuka posisi baharu).
- **Persetujuan penafian berversikan** — penerimaan sekali, berversikan diperlukan untuk mengaktifkan Auto Penuh
  (persetujuan yang diwartakan secara sah, bukan kelulusan setiap dagangan); menukar penafian memaksa persetujuan semula.
- **Suis bunuh** — berhenti idle ejen idempoten pada setiap ejen yang berjalan.

## Matlamat

Beri ejen **matlamat yang boleh diukur** — cth *jaga undur max di bawah 4%*, *faktor keuntungan sekurang-kurangnya
1.5*, *kadar menang ≥ 55%*. Setiap sasaran ialah **Keras** (pengawal keselamatan — pelanggaran mencetuskan pemutus litar) atau
**Lembut** (hanya mengarahkan penaakulan), dinilai sebagai Ber-landung / Berisiko / Dilanggar.

## Talian keputusan

Setelah bermula, ejen menjalankan **gelung terkawal 24/7** (`AgentRuntimeService`). Setiap tik, untuk setiap
akaun yang diurus, nó: membaca **keadaan akaun deterministik** (kebenaran-ground, bukan ingatan model);
meminta enjin keputusan untuk satu langkah; melaluinya melalui **gerbang keselamatan** (`AgentDecisionProcessor`) —
paras autonomi → pemutus litar → sampul risiko; menulis `AgentDecisionRecord` hanya-tambah; dan
berhenti atau dilaksanakan seperti yang diarahkan gerbang. Gelung **terasing kesalahan** (kegagalan satu ejen tidak pernah menyentuh
ejen lain atau hos) dan **selamat secara lalai**: ia adalah inert melainkan AI dikonfigurasi *dan*
`App:Ai:AgentRuntimeEnabled` ditetapkan, dan nó tidak pernah membuka risiko baharu semasa pembekal AI tidak tersedia.

- **Gerbang kelulusan** — pesanan yang dicadangkan ejen **Kelulusan-digerbang** direkod sebagai **Tertunda** dan tidak
  melakukan apa-apa sehingga pemilik meluluskannya (`POST /api/agent-studio/{id}/decisions/{seq}/approve` atau
  `/reject`); **Auto Penuh** jelas melalui sampul tanpa kelulusan setiap dagangan; **Nasihat** hanya mencadangkan.
- **Buku audit** — setiap keputusan boleh diputar semula: penaakulan (XAI), bukti nó cited, verdict gerbang,
  niat pesanan dan sama ada ia dilaksanakan, di `GET /api/agent-studio/{id}/decisions`.
- **Meja kajian** — debat ejen berbilang atas-demand: analyst Alpha/Sentimen/Teknikal/Risiko masing-masing memberikan
  pandangan dan Penyemak mensintesis cadangan (`POST /api/agent-studio/{id}/debate`).
- **Memori** — ejen ingat setiap keputusan dan mengingat memori terkini ke prompt seterusny untuk kesinambungan
  (`GET /api/agent-studio/{id}/memory`).

Setiap baris barisan **Butiran** membuka suapan keputusan ejen (dengan Lulus/Tolak pada pesanan tertunda),
memori nó, dan tab Lari-debat.

## Skop

Dihantar: kitaran hayat ejen penuh, gerbang keselamatan deterministik, masa jalan 24/7, gerbang kelulusan manusia-dalam-gelung,
buku audit, dan **integrasi langsung cTrader Open API** — kedai keadaan akaun (membaca baki sebenar, posisi dan pendedahan terbuka dalam lot) dan pelaksanaan pesanan (meletakkan pesanan pasaran sebenar, lot→volum melalui saiz lot simbol), kedua-duanya menyelesaikan bukti OAuth setiap akaun yang diurus dan mundur dengan selamat apabila akaun tidak dipautkan. **Memerlukan kunci API Anthropic** untuk model menjana pesanan (sehingga nó enjin tahan); yang masih akan datang ialah peranan debat ejen berbilang dan ingatan/ refleksi berlapis. Masa jalan dimatikan melainkan `App:Ai:AgentRuntimeEnabled` ditetapkan, jadi perdagangan langsung hanya
berlaku pada pilihan masuk yang dipersetujui sepenuhnya.

## Akaun yang diurus dan penyuntingan

Apabila cipta ejen anda pilih akaun perdagangan yang ia urus — **sekurang-kurangnya satu diperlukan pada penciptaan** (butang *Cipta* dilumpuhkan sehingga satu dipilih, dan titik akhir cipta menolak pemilihan kosong). Setiap ejen boleh **diedit** selepas itu (nama, temperamen, autonomi, dan akaun yang diurus) daripada ikon pensil pada baris barisan nó. Kawalan kitaran hayat (butiran, edit, mula, henti, bunuh) ialah butang ikon, setiap dilumpuhkan dalam keadaan di mana tindakan tidak terpakai.
