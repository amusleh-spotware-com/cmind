---
description: "Agent Studio — buat agent trading tanpa kode dengan karakter dan archetype yang mengelola akun menuju tujuan Anda di bawah Autonomy & Safety Kernel (amplop risiko, circuit breaker, kill switch, persetujuan disclaimer berversion)."
---

# Agent Studio

Agent Studio memungkinkan Anda membuat **agent trading dengan karakter** — tanpa kode — dan memberikan
pengelolaan akun kepada tujuan terukur. Agent seperti cBot berbasis personality: Anda memilih archetype
dan attitude, menetapkan guardrail, dan agent berjalan di bawah **Autonomy & Safety Kernel**.

Buka **AI → Agent Studio** (`/agent-studio`).

## Buat agent

Dialog **Agent baru** mengumpulkan, tanpa kode:

- **Nama** dan **archetype** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion, atau Breakout/Momentum. Setiap preset menetapkan cadence dan posture
  yang masuk akal.
- **Attitude** — slider agresivitas, kesabaran, dan trend-following.
- **Akun yang dikelola** — **minimal satu diperlukan untuk membuat agent** (agent tanpa akun tidak pernah bisa dimulai, jadi *Create* tetap disabled hingga Anda pilih satu). Jika Anda belum menautkan akun trading, dialog mengatakan demikian dan mengarahkan Anda ke link satu terlebih dahulu.
- **Tingkat otonomi** — **Advisory** (hanya propose) atau **Approval-gated** (bertindak hanya setelah
  persetujuan per-aksi). **Full Auto** (tanpa persetujuan per-trade) tambahan memerlukan **risk envelope**
  dan penerimaan risk disclaimer sebelum bisa diaktifkan.

Persona dikompilasi **deterministically** ke system prompt agent (bukan LLM yang menuliskannya), sehingga
konfigurasi yang sama selalu menghasilkan instruksi yang sama — reprodusible dan auditable.

## Daftar agent

Setiap agent ditampilkan dalam tabel control-room: **agent mana, tipenya, berapa banyak akun yang
dikelolanya, tujuannya, status berjalan, dan aksi terakhir**, dengan kontrol **Start / Stop / Kill**.
Kill switch menghentikan agent yang berjalan segera.

## Keamanan adalah invariant domain, bukan pengaturan

Semua hal yang menyentuh uang dirutekan melalui **Autonomy & Safety Kernel**:

- **Risk envelope** — batas keras per-order (max daily loss, open exposure, position size, leverage,
  konsekutif losses, orders/jam, simbol yang diizinkan). Setiap order divalidasi terhadap envelope
  sebelum dispatch; pelanggaran ditolak, bukan di-clamp. Diperlukan sebelum agent bisa mencapai
  Full Auto.
- **Circuit breaker** — halt deterministik pada losing streak, pelanggaran daily-loss, **pelanggaran
  performance-goal keras**, atau **ketidaktersediaan AI provider** (model down atau halluncinating
  tidak pernah membuka posisi baru).
- **Versioned disclaimer consent** — penerimaan one-time dan berversion diperlukan untuk mengaktifkan
  Full Auto (persetujuan yang secara hukum diperlukan, bukan persetujuan per-trade); menambah
  disclaimer memaksa re-consent.
- **Kill switch** — halt darurat idempoten pada setiap agent yang berjalan.

## Tujuan

Beri agent **tujuan terukur** — misalnya *jaga max drawdown di bawah 4%*, *profit factor minimal
1.5*, *win rate ≥ 55%*. Setiap target adalah **Hard** (guardrail — pelanggaran memicu circuit
breaker) atau **Soft** (hanya mengarahkan penalaran), dievaluasi sebagai On-track / At-risk /
Breached.

## Pipeline keputusan

Setelah dimulai, agent menjalankan **loop supervised 24/7** (`AgentRuntimeService`). Setiap tick, untuk
setiap akun yang dikelola, ia: membaca **deterministic account state** (ground truth, bukan memory
model); meminta decision engine untuk langkah; melewatkannya melalui **safety gate**
(`AgentDecisionProcessor`) — autonomy level → circuit breaker → risk envelope; menulis
**`AgentDecisionRecord`** append-only; dan halt atau eksekusi sesuai gate. Loop **fault-isolated**
(kegagalan satu agent tidak pernah menyentuh yang lain atau host) dan **safe by default**: inert
kecuali AI dikonfigurasi *dan* `App:Ai:AgentRuntimeEnabled` diset, dan tidak pernah membuka risiko
baru saat AI provider tidak tersedia.

- **Approval gate** — order yang di-propose agent dengan **Approval-gated** direkam sebagai **Pending**
  dan tidak melakukan apa-apa sampai owner menyetujui (`POST /api/agent-studio/{id}/decisions/{seq}/approve`
  atau `/reject`); **Full Auto** melewati envelope tanpa persetujuan per-trade; **Advisory** hanya
  propose.
- **Audit ledger** — setiap keputusan dapat di-replay: penalaran (XAI), bukti yang dikutip, verdict
  gate, intent order dan apakah dieksekusi, di `GET /api/agent-studio/{id}/decisions`.
- **Research desk** — debat multi-agent on-demand: analyst Alpha/Sentiment/Technical/Risk masing-masing
  memberikan pandangan dan Reviewer mensintesis proposal (`POST /api/agent-studio/{id}/debate`).
- **Memory** — agent mengingat setiap keputusan dan memanggil memory baru ke prompt berikutnya untuk
  kontinuitas (`GET /api/agent-studio/{id}/memory`).

Setiap baris roster **Details** membuka feed keputusan agent (dengan Approve/Reject pada order pending),
memory, dan tab Run-debate.

## Cakupan

Sudah dikirim: lifecycle agent penuh, deterministic safety gate, runtime 24/7, human-in-the-loop
approval gate, audit ledger, dan **integrasi cTrader Open API langsung** — account-state store
(membaca balance nyata, posisi dan open exposure dalam lot) dan order executor (menempatkan order
market nyata, lot→volume via lot size simbol), keduanya me-resolve kredensial OAuth setiap akun yang
dikelola dan menurun dengan aman ketika akun tidak tertaut. **Membutuhkan API key Anthropic** agar
model menghasilkan order (sampai saat itu engine hold); yang masih akan datang adalah multi-agent
debate roles dan layered memory/reflection. Runtime off kecuali `App:Ai:AgentRuntimeEnabled` diset,
sehingga live trading hanya terjadi pada opt-in yang eksplisit dan sepenuhnya disetujui.

## Akun yang dikelola dan pengeditan

Saat membuat agent Anda memilih akun trading yang dikelolanya — **minimal satu diperlukan saat creation** (tombol *Create* disabled hingga satu dipilih, dan create endpoint menolak empty selection). Setiap agent dapat **diedit** kemudian (nama, temperament, autonomy, dan managed accounts) dari ikon pencil pada baris roster. Lifecycle controls (details, edit, start, stop, kill) adalah icon buttons, masing-masing disabled dalam states di mana action tidak berlaku.
