---
description: "GitHub Sürümleri: sürümlenmiş konteyner imajları (GHCR), Helm chart ve CtraderCliNode ikili dosyaları — bir sürümü alma ve uygulamayı ondan çalıştırma."
---

# Sürümler ve bir sürümü çalıştırma

cMind, sürümlenmiş **GitHub Sürümleri** olarak dağıtılır. Her sürüm, tek bir SemVer etiketi için şunları yayınlar:

- **Konteyner imajları** (GHCR) — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  sürümle (ör. `1.0.0-alpha.1`) ve `sha-<commit>` ile etiketlenir. Derleme kaynağı doğrulamaları ve SPDX
  SBOM ile imzalanmıştır (cosign keyless).
- **Helm chart** — `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` adresine gönderilir ve sürüme
  `cmind-<version>.tgz` olarak eklenir.
- **CtraderCliNode ikili dosyaları** — .NET SDK olmadan uzak bir düğüm aracısı çalıştırmak için platforma
  özel bağımsız ZIP'ler (`linux-x64`, `linux-arm64`, `win-x64`, `osx-arm64`).
- Eklenen her yapıyı kapsayan **`SHA256SUMS.txt`**.

> **Alpha.** Şimdilik her sürüm bir ön sürümdür (`-alpha.N`). Alpha'lar arasında kırıcı değişiklikler
> bekleyin; henüz yükseltme/geçiş garantisi yoktur. Kesin bir sürüme sabitleyin — asla `latest` kullanmayın.

## Sürümleme

SemVer 2.0.0. Etiket biçimi `vX.Y.Z[-suffix]`. Bir sonek (`-alpha.N`, `-beta.N`, `-rc.N`) bir GitHub
**ön sürümü** yayınlar; imaj etiketi ve Helm chart sürümü, baştaki `v` olmadan sürüme eşittir. Çalışan
uygulama bunu `GET /version` ve UI alt bilgisinde (`Core.VersionInfo`) gösterir.

## Bir sürüm seçme

**[Sürümler](https://github.com/amusleh-spotware-com/cmind/releases)** sayfasına göz atın ve istediğiniz
etiketi (ör. `v1.0.0-alpha.1`) kopyalayın. Çalıştırmadan önce bir imajı doğrulayın:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Çalıştırma — Kubernetes (Helm, önerilir)

Chart'ın `appVersion` değeri, eşleşen imaj etiketini zaten sabitler; bu nedenle yalnızca chart sürümünü
geçirirsiniz.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<32+ karakterlik küme sırrı>'
```

Özel GHCR paketleri bir imaj çekme sırrı gerektirir — bir tane oluşturup geçirin:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<read:packages izinli PAT>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Tüm chart seçenekleri, ingress, harici Postgres ve ölçeklendirme: **[Kubernetes dağıtımı](kubernetes.md)**
ve **[Ölçeklendirme](scaling.md)** bölümlerine bakın. Doğrulama:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version sürüm numarasını döndürür
```

## Çalıştırma — Docker (tek ana makine, hızlı bakış)

Web ana makinesini doğrudan sürüm imajından çalıştırın. Postgres ve Docker soketi gerektirir (Web ana
makinesi cBot'ları yerel Docker CLI aracılığıyla derler/çalıştırır).

```bash
VERSION=1.0.0-alpha.1
docker network create cmind

docker run -d --name cmind-pg --network cmind \
  -e POSTGRES_PASSWORD=change-me -e POSTGRES_DB=cmind postgres:17

docker run -d --name cmind-web --network cmind -p 8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e ConnectionStrings__Default='Host=cmind-pg;Database=cmind;Username=postgres;Password=change-me' \
  -e App__Owner__Email='owner@example.com' \
  -e App__Owner__Password='Change-Me-Str0ng!' \
  ghcr.io/amusleh-spotware-com/cmind-web:$VERSION
```

`http://localhost:8080` adresini açın. MCP sunucusunu (`cmind-mcp`) ve düğüm aracılarını aynı şekilde
ekleyin; tam çok hizmetli topoloji için Helm chart'ı kullanın. Bir sürüm yerine kaynaktan çalışırken Aspire
`dotnet run` yolu için **[Yerel geliştirme](local.md)** bölümüne bakın.

## Bir ikili dosyadan uzak düğüm aracısı çalıştırma

Çalıştırma/geriye dönük test kapasitesi sağlayan uzak ana makineler, .NET kurulu olmadan `CtraderCliNode`
çalıştırabilir. Sürümden platform ZIP'ini indirin, açın ve çalıştırın — Web ana makinesine kendini otomatik
kaydeder ve heartbeat gönderir.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<web-ana-makineniz>' \
NodeAgent__DiscoveryJoinToken='<aynı 32+ karakterlik küme sırrı>' \
./CtraderCliNode
```

Ana makinede Docker çalışmalıdır (aracı, cTrader konsol imajını Docker CLI aracılığıyla çalıştırır). Düğüm
aracılarını ayrıcalıklı pod'lar olarak çalıştırmak için **[Kubernetes dağıtımı](kubernetes.md)** bölümüne
bakın.

## Bir sürüm oluşturma (bakımcılar)

Sürümler, gönderilen herhangi bir `v*` etiketinde `.github/workflows/release.yml` tarafından üretilir —
süreç, depo kökündeki
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** içindedir.
