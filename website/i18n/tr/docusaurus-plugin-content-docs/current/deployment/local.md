---
title: Yerel olarak çalıştırın
description: Docker Compose (veya geliştirme için .NET Aspire) ile cMind'ı kendi makinenizde birkaç dakikada çalıştırın.
sidebar_position: 1
---

# cMind'ı yerel olarak çalıştırın 🖥️

Bu cMind'ı gerçekten görmenin en hızlı yolu — kendi makinenizde tam bir örnek. Kahve alın;
soğumadan önce muhtemelen oturum açmış olabilirsiniz.

:::tip Sonunda ne yapacaksınız
**localhost:8080** 'de çalışan bir web uygulaması, **localhost:8081** 'de bir MCP sunucusu, bir Postgres
veri tabanı ve cBot'ları derlemek ve backtest yapmak için hazır yerel bir çalışan düğümü. Tümü
makinenizde, tümü sizin.
:::

**Başlamadan önce, bunlardan birini almanız gerekir:**

- **Sadece Docker** → Seçenek A'yı kullanın (.NET SDK gerekli değildir). İlk bakış için önerilir.
- **.NET 10 SDK + Docker** → Kodu hacklemeyi istiyorsanız Seçenek B'yi kullanın.

Her iki yol da çapraz platform (Windows / macOS / Linux).

## Seçenek A — Docker Compose (.NET SDK gerekli değildir)

Ön koşul: Docker Desktop (veya Docker Engine + compose eklentisi).

```bash
cp .env.example .env        # PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD'ı düzenleyin
docker compose up --build
```

- Web UI: <http://localhost:8080> (sahibi ile `.env` oturum aç; ilk oturum açımda şifre değiştirilmeye zorla).
- MCP sunucusu: <http://localhost:8081/mcp>.
- Postgres veriler `pgdata` sesine devam eder; şema başlangıçta otomatik olarak geçer.

Web konteyner ana bilgisayar Docker soketini (`/var/run/docker.sock`) bağlayarak tarayıcıda bulunan
kurucu ve tohumlanmış **LocalNode** derleme ve makinenizde cTrader Console konteynerler
çalıştırır.

**Çapraz platform notları**
- Docker Desktop (Windows/macOS) soketini `/var/run/docker.sock` adresine maruz bırakır — compose
  bağlama olduğu gibi çalışır.
- Linux: kullanıcının soketine erişebileceğinden emin olun veya yeterli ayrıcalıklarla compose'i
  çalıştırın.
- Web görüntüsü `linux/amd64` 'dür; Apple Silicon'da Docker bunu emülasyon altında çalıştırır.

Durdur ve sil:

```bash
docker compose down          # veriyi tut
docker compose down -v       # veritabanı sesini de sil
```

## Seçenek B — .NET Aspire (geliştirme için)

Ön koşul: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire, Postgres, Web, MCP, pgAdmin'i düzenler; bağlantı dizelerini + OTLP'yi yönetir; panoyu açar. Sahibi
kimlik bilgilerini Aspire parametreleri (`OwnerEmail`, `OwnerPassword`) olarak ayarlayın.

Web uygulamasını sadece mevcut Postgres'e karşı çalıştırın:

```bash
dotnet run --project src/Web
```

## Düğümleri yerel olarak ekleme

Tohumlanmış LocalNode zaten makinenizde çalışma çalıştırır. **Otomatik keşif** 'i yerel olarak alıştırmak
için `NodeAgent:MainUrl=http://host.docker.internal:8080` ve eşleşen `JoinToken` ile Web uygulamasını
gösteren düğüm aracısını başlatın ([düğüm keşfi](../operations/node-discovery.md) bkz.).

## Sorun Giderme 🔧

Docker görüşleri vardır. İşte olağan şüpheliler:

| Belirti | Muhtemel neden ve düzeltme |
|---|---|
| `port is already allocated` (8080/8081) | Başka bir şey bağlantı noktasını kullanıyor. Durdurun veya
`docker-compose.yml` içinde eşlemeyi değiştirin. |
| Web başlar ancak derleme/backtest başarısız olur | Docker soketi bağlanmış veya erişilebilir değildir. Linux'ta,
kullanıcınızın `/var/run/docker.sock` 'e ulaşabildiğinden emin olun. |
| Soket üzerinde `permission denied` (Linux) | Kullanıcınızı `docker` grubuna ekleyin (`sudo usermod -aG docker
$USER`) ve yeniden oturum açın veya yeterli ayrıcalıklarla çalıştırın. |
| Çok yavaş ilk çalıştırma | İlk derleme görüntüleri çeker ve derler — sonraki çalıştırmalar çok daha hızlıdır.
Apple Silicon'da `linux/amd64` web görüntüsü emülasyon altında çalışır. |
