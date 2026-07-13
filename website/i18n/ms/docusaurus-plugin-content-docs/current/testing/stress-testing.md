---
description: "Suite tekanan. Menghentam bahagian apl yang kegagalannya merugikan pengguna wang — terutamanya salinan perdagangan — dengan beban kerja/pendedahan过失 yang diacak, injective. Meng.assert system kekal betul."
---

# Ujian tekanan

Suite tekanan. Menghentam bahagian apl yang kegagalannya merugikan pengguna wang — terutamanya **salinan perdagangan** — dengan beban kerja/pendedahan过失 yang diacak, disuntik. Meng.assert sistem kekal betul. Tinggal dalam `tests/StressTests`, lari dalam bina `dotnet test` green gate.

## Pendekatan — Ujian Simulasi Deterministik (DST)

Cara terbaik untuk menekan sistem keuangan terdistribusi = **ujian simulasi deterministik**, menurut TigerBeetle, FoundationDB, Antithesis: jalankan logik sebenar terhadap *dunia* yang disimulasikan, gunakan beban kerja **seeded** rawak + faults yang disuntik, assert invariant pada quiscence. Semua seeded + deterministik → sebarang kegagalan mereproduksi tepat dari seed. Digabungkan dengan:

- **Injksi fault chaos-engineering** (gaya Netflix Chaos Monkey) — sambungan jatuh, penolakan pesanan, putaran token, kematian nod.
- **Invariant berasaskan hartanah** — tidak assert jujukan panggilan tepat; assert hartanah yang harus pegang tidak kira bagaimana peristiwa bersilang (penumpuan, tiada orphan, paling satulesen pemunya).

Apl sudah pengiriman dunia ujian DST yang sempurna: `FakeTradingSession`, sesi Open API cTrader dalam-memori yang setia. Suite tekanan использует ulang nó (dipaut, satu sumber kebenaran) bukan tiruan, jadi broker simulasi berkelakuan seperti broker sebenar.

## Apa yang diliputi

### Salinan perdagangan (fokus utama)

Digerakkan melalui `CopyDstWorld` (`tests/StressTests/CopyTrading/`), menjalankan `CopyEngineHost` live terhadap sesi palsu, mengeluarkan beban kerja sumber yang konsisten denganKeahlian:

| Senario | Mengehadkan |
|---|---|
| `Mass_fan_out…` | 1 sumber → 80 destinasi, 150 buka kemudian tutup; fan-out penuh + saliran |
| `High_frequency_open_close…` | 300 terbuka/tutup selang; tiada posisi bocor |
| `Partial_close_and_scale_in_storm…` | hujan tutup separa + scale-in; kestabilan set label |
| `Connection_flap_storm…` | putuskan/sambung semula berulang + nycersync tengah-terbang; penumpuan resync |
| `Order_rejection_cascade…` | sebahagian menolak setiap pesanan; destinasi sihat tidak terjejas, kemudian sembuh sendiri melalui resync |
| `Token_rotation_storm…` | swap token di tempat yang cepat semasa ribut pesanan |
| `Randomized_chaos_workload…` (10 seeds) | **Dst teras** — setiap jenis peristiwa + setiap fault diselang secara rawak |
| `CopyLeaseReclaimStressTests` | kematian nod + ambil semula lesen merentasi kluster diskala (domain murni, `FakeTimeProvider`) |

**Invariant penumpuan.** Pada rehat, setiap destinasi sihat mencerminkan tepat set posisi terbuka sumber yang masih buka — tiada orphan, tiada yang hilang. Diassert pada set label (scale-in secara legit membuka posisi destinasi kedua di bawah label sumber yang sama, jadi label pendua dijangka). Destinasi yang sedang menolak pesanan dibenarkan ketinggalan, disejajarkan sebaik sembuh.

**Invariant lesen.** Dalam kluster di mana nod mati + revive pada jadual seeded, paling satu nod pernah memegang lesen sah pada profil; nod mati lesennya tamat tepat pada tamat, dituntut semula; kluster sihat diselesaikan dengan setiap profil dipegang oleh tepat satu nod. Mencerminkan tuntutan predikat `CopyEngineSupervisor` terhadap kaedah lesen domain `CopyProfile`.

## Keselamatan benang harness

`FakeTradingSession` single-threaded; beban tekanan bermutasi nó dari utas ujian semasa hos membaca/menulis dari gelungnya. `SyncTradingSession` bungkusnya, menjadikan setiap operasi sesi atom pada satu pagar (tanpa memegang pagar merentasi panggilan balik sambung semula — akan songsang urutan kunci dengan `_stateGate` hos dan deadlock). Simulator sendiri tidak disentuh.

## Pepijat ditemui

- **Kecurian permulaan resync dalam `CopyEngineHost`.** `OnReconnected` disambung sebelum muat rujukan awal + resync pertama, yang lari tanpa `_stateGate`. Soket flap semasa permulaan menjalankan resync kedua secara selari, memesongkan dict keadaan bukan konkuren hos (`_symbolDetails`, `_sourceVolumes`). Diperbaiki: jalankan muat permulaan + resync pertama di bawah pagar. Race pengeluaran, bukan artifak ujian — beban kerja chaos DST memaparkannya.

## Menjalankan

```bash
dotnet test tests/StressTests/StressTests.csproj
```

Suite **diserializer** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`): setiap ujian memutar gelung hos live, mengemudi ke quiscence di bawah jam dinding, jadi lari selari membiarkan tugas hos kelaparan dan menjadikan masa penumpuan masa tamat jadi flaky. Beban kerja bersaiz untuk disiapkan dalam saat jadi suite kekal dalam green gate lalai. Kegagalan cetak nó它们; ulangi nó它们 untuk reproduce imbangan peristiwa yang tepat.

## Memperluaskan

- Kelakuan salinan baharu → tambah op sumber kepada `CopyDstWorld` (kekalkan keahlian buku sumber konsisten dengan strim peristiwa) + kes bernisbah dalam `CopyChaosDstTests`. Jika nó boleh cipta atau bersara posisi destinasi, pastikan invariant penumpuan masih pegang.
- Fault baharu → tambah injector kepada `CopyDstWorld` (delegat kepada kawalan permukaan `FakeTradingSession` melalui `SyncTradingSession`) + berlatih dalam senario bernama ditambah campuran chaos.
- Kekalkan simulator setia kepada cTrader (lihat mandat `CLAUDE.md` root); tidak pernah meredakan nó untuk melepaskan ujian tekanan.
