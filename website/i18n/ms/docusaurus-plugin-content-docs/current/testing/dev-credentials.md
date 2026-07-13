---
description: "Semua kredensi yang diperlukan suite ujian tinggal dalam satu fail gitignored: secrets/dev-credentials.local.json. Salin templat yang komited dan isikan apa yang anda ada — setiap nilai adalah pilihan dan ujian yang memerlukan nilai yang hilang dilangkau dengan kemas."
---

# Kredensi dev — satu fail untuk setiap ujian

Semua kredensi yang diperlukan suite ujian tinggal dalam satu fail gitignored:
`secrets/dev-credentials.local.json`. Salin templat yang dikomit dan isikan apa yang anda
ada — setiap nilai adalah pilihan dan ujian yang memerlukan nilai yang hilang dilangkau dengan kemas.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# edit secrets/dev-credentials.local.json
```

## Apa yang dibaca setiap lapisan ujian

| Lapisan | Perlukan | Dari |
|------|-------|-------|
| **Unit** (`tests/UnitTests`) | tiada apa-apa | — deterministik, tiada rahsia, tiada rangkaian |
| **Integrasi** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — automatik |
| **Salinan langsung** (`tests/IntegrationTests/CopyLive`) | Apl OpenAPI + cache token | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E aboard** (`tests/E2ETests/CopyLive`) | Apl OpenAPI + log masuk cID | `OpenApi.App`, `OpenApi.Cids` |
| **E2E lari/backtest sebenar** (`CBotRealRunBacktestTests`) | Log masuk cID + nombor akaun **demo** | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **Ciri AI** | Kunci Anthropic | `Ai.ApiKey` (tetapkan ⇒ ciri AI kembalikan dilumpuhkan, apl masih berjalan) |

## Skima

Lihat `dev-credentials.example.json` di akar repo. Bahagian:

- `OpenApi.App` — `{ ClientId, ClientSecret }` aplikasi cTrader Open API.
- `OpenApi.Cids` — log masuk ID cTrader yang digunakan oleh aboard utan pelayar. Setiap entri juga membawa tatasusunan **`Accounts`** — nombor akaun perdagangan cTrader (nombor log masuk/akaun,
  cth `3635817`) di bawah cID itu yang infrastuktur ujian dibenarkan untuk memaut ke apl dan
  mengawal. `CBotRealRunBacktestTests` membaca entri pertama yang mempunyai tatasusunan `Accounts` bukan kosong,
  menambah cID + akaun itu ke apl, kemudian benar-benar Melarikan dan backtest cBot pad nó. **Letakkan hanya
  nombor akaun demo di sini** — tidak pernah akaun langsung; ujian lari/backtest meletakkan pesanan sebenar pada
  mana-mana akaun yang anda senaraikan. `Accounts` kosong/dihilang ⇒ ujian lari/backtest sebenar melangkau dengan kemas.
- `OpenApi.Tokens` — cache token pelbagai cID (satu entri setiap cID yang dibenarkan dengan akses/refresh
  token + senarai akaun). Ditulis secara automatik oleh aboard dan oleh langkah penyegaran token; anda jarang mengedit nó secara manual.
- `Owner` — log masuk pemilik Seed untuk apl di bawah E2E.
- `Database.ConnectionString` — hanya apabila mengarahkan ujian kepada Postgres luaran berbanding
  Testcontainers.
- `Ai.ApiKey` — kunci API Anthropic untuk ciri AI.

## Keutamaan

1. **Pembolehubah persekitaran** mengatasi segalanya (cth `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — fail bersatu (pilihan).
3. **Fail legacy split** — `openapi-test-app.local.json`, `openapi-cids.local.json`,
   `openapi-tokens.local.json` masih dibaca apabila fail bersatu tiada, jadi mesin sedia ada
   terus berfungsi. Persediaan baharu harus gunakan fail tunggal.

## Keselamatan

- `secrets/` dan `*.local.json` di-gitignore — tidak ada di sini yang pernah dikomit.
- Ujian salinan langsung enggan lari terhadap akaun bukan demo (`IsLive` ditapis
  oleh `LiveCopyFixture`). Simpan hanya akaun demo dalam cache token.
- Dalam-kluster (Kubernetes) lari memasang fail sebagai Rahsia baca sahaja; penyegaran token
  disimpan dalam memori dan tulis balik baca sahaja ialah no-op senyap.
