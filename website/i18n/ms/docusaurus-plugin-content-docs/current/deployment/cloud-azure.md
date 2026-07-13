---
description: "deploy/azure/main.bicep memperuntuk lapisan tanpa negara pada Azure Container Apps tambah Postgres Pelayan Fleksibel + Log Analytics."
---

# Penempatan Azure — langkah demi langkah

`deploy/azure/main.bicep` memperuntuk lapisan tanpa negara pada **Azure Container Apps** tambah **Postgres Pelayan Fleksibel** + Log Analytics.

## 1. Prasyarat

- Azure CLI (`az login` selesai), langganan, kebenaran untuk membuat kumpulan sumber.
- Tiga imej ditolak ke pendaftar Azure boleh tarik (contohnya GHCR awam, atau ACR).

## 2. Buat kumpulan sumber

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Gunakan Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Mencipta: Persekitaran Container Apps, Web (kemasukan luaran), MCP (kemasukan luaran), Pelayan Postgres Fleksibel + `appdb`, Log Analytics, komponen **Application Insights berasaskan ruang kerja**. Penemuan aktif untuk Web. Rentetan sambungan-nya disuntik ke Web + MCP sebagai `APPLICATIONINSIGHTS_CONNECTION_STRING`, jadi jejak + metrik mengeksport secara asli ke App Insights manakala log mendarat dalam ruang kerja Log Analytics yang sama — tiada pengumpul diperlukan. Lalui `-p otlpEndpoint=...` untuk *juga* menghantar ke pengumpul OTLP.

## 4. Dapatkan URL

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Buka `webUrl`, daftar masuk dengan pemilik (perubahan kata laluan terpaksa pada log masuk pertama).

## 5. Tambah ejen nod (berasingan)

Container Apps tidak boleh menjalankan istimewa/DinD, jadi jalankan ejen di tempat lain, sela pada `webUrl`:

- **AKS** — gunakan Carta Helm ([kubernetes.md](kubernetes.md)) dengan `nodeAgent.privileged=true`, skala Web/MCP ke 0 jika mahu lapisan ejen sahaja di sana.
- **VM / VMSS** — jalankan imej `cmind-node-agent` `--privileged` dengan `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Ejen mendaftar sendiri dalam selang rentak satu — lihat [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Sahkan

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # log JSON padat
curl -s <webUrl>/version
```

## Catatan pengeluaran

- Permukaan Web dengan Pintu Depan Azure / App Gateway untuk TLS + WAF.
- Simpan rahsia di Peti Kunci; lalui sijil DataProtection stabil (`App__DataProtectionCertBase64` / `...Password`) supaya gelang kunci kekal hidup selepas permulaan replika.
- App Insights (jejak+metrik) + Log Analytics (log) berwayar secara automatik; korelasikan pada `trace_id`. Lihat [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Tetapkan param `otlpEndpoint` (atau `OTEL_EXPORTER_OTLP_ENDPOINT` pada apl) untuk *juga* menghantar ke pengumpul.
- Peraturan `scale` Container Apps (min/maks) berwayar dalam Bicep.

## Ejen salinan perdagangan + Peti Kunci (S5)

`deploy/azure/main.bicep` juga memperuntuk **copy-agent** Container App menganjurkan `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) dengan **tiada kemasukan** — pekerja memegang soket cTrader berduration panjang. Membaca rentetan sambungan DB daripada rahsia **Azure Key Vault** melalui **identiti terurus yang ditugaskan pengguna** (peranan Pengguna Rahsia Peti Kunci) daripada rahsia pautan teks biasa. Setiap replika `NodeName` lalai ke nama hos kontena-nya (unik), jadi atribut pajakan DB menjalankan profil setiap replika dan dua replika tidak pernah melayan dua host satu. Skala `minReplicas`/`maxReplicas` untuk menambah kapasiti salinan; gelang kunci DataProtection dikongsi melalui Postgres, jadi mana-mana replika boleh menguraikan token Open API yang disimpan. Output: `copyAgentName`, `keyVaultName`.
