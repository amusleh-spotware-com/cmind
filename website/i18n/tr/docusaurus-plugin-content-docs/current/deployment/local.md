---
title: Yerel olarak çalıştırın
description: Docker Compose ile (veya geliştirme için .NET Aspire) cMind'i kendi makinenizde birkaç dakikada çalıştırın.
sidebar_position: 1
---

# cMind'i Yerel Olarak Çalıştırın 🖥️

Bu, cMind'i gerçekten görmenin en hızlı yolu — kendi makinenizde tam bir örnek. Kahve alın; muhtemelen soğumadan önce oturmuş olursunuz.

:::tip Sonunda sahip olacağınız şey
**localhost:8080** adresinde çalışan bir web uygulaması, **localhost:8081** adresinde bir MCP sunucusu, bir Postgres veritabanı ve cBot'ları derlemek ve test etmeye hazır yerel bir çalışan düğümü. Hepsi makinenizde, hepsi sizin.
:::

**Başlamadan önce, birinden biriyle ihtiyacınız var:**

- **Sadece Docker** → Seçenek A'yı kullanın (.NET SDK gerekli değil). İlk bakış için önerilir.
- **.NET 10 SDK + Docker** → Kod üzerinde çalışmak istiyorsanız Seçenek B'yi kullanın.

Her iki yol da platformlar arası (Windows / macOS / Linux).

## Seçenek A — Docker Compose (.NET SDK gerekli değil)

Ön koşul: Docker Desktop (veya Docker Engine + compose eklentisi).

```bash
cp .env.example .env        # PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD'u düzenleyin
docker compose up --build
```

- Web UI: <http://localhost:8080> (`.env` adresinden sahip ile oturum açın; ilk oturum açmada parolayı değiştirmeye zorlanan).
- MCP sunucusu: <http://localhost:8081/mcp>.
- Postgres verileri `pgdata` biriminde kalıcıdır; şema başlangıçta otomatik olarak göç eder.

Web konteyneri ana bilgisayar Docker soketini (`/var/run/docker.sock`) bağlar, bu nedenle tarayıcı içi oluşturucu ve tohumlanmış **LocalNode** makinenizde cTrader Console konteynerleri derler ve çalıştırır.

**Platformlar arası notlar**
- Docker Desktop (Windows/macOS) soketi `/var/run/docker.sock` adresinde ortaya koyar — compose bağlama olduğu gibi çalışır.
- Linux: kullanıcınızın soket erişim izni olduğundan emin olun veya yeterli ayrıcalıklarla compose çalıştırın.
- Web görüntüsü `linux/amd64` olur; Apple Silicon üzerinde Docker bunu emülasyonda çalıştırır.

Durdur ve temizle:

```bash
docker compose down          # verileri tut
docker compose down -v       # veritabanı birimini de sil
```

## Seçenek B — .NET Aspire (geliştirme için)

Ön koşul: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire, Postgres, Web, MCP, pgAdmin'i orkestrasyonu; bağlantı dizelerini + OTLP'yi bağlar; pano açar. Sahip kimlik bilgilerini Aspire parametreleri (`OwnerEmail`, `OwnerPassword`) olarak ayarlayın.

Varolan Postgres'e karşı sadece web uygulamasını çalıştırın:

```bash
dotnet run --project src/Web
```

## Çalışan düğümleri yerel olarak ekleme

Tohumlanmış LocalNode zaten makinenizde çalışma çalıştırır. **Otomatik keşfi** yerel olarak alıştırmak için, Web uygulamasını işaret eden düğüm aracısını başlatın ([düğüm keşfi](../operations/node-discovery.md) bölümüne bakın) `NodeAgent:MainUrl=http://host.docker.internal:8080` ve eşleşen `JoinToken` ile.

## Sorun Giderme 🔧

Docker fikirler vardır. İşte olağan şüpheliler:

| Belirti | Olası sebep ve çözüm |
|---|---|
| `port is already allocated` 8080/8081 üzerinde | Başka bir şey portu kullanıyor. Durdurun veya `docker-compose.yml` adresindeki haritayı değiştirin. |
| Web başlar ancak derlemeler/backtestler başarısız olur | Docker soketi monte edilmemiş veya erişilemez. Linux'ta, kullanıcınızın `/var/run/docker.sock` adresine erişip erişemeyeceğini kontrol edin. |
| `permission denied` sokette (Linux) | Kullanıcınızı `docker` grubuna ekleyin (`sudo usermod -aG docker $USER`) ve yeniden oturum açın veya yeterli ayrıcalıklarla çalıştırın. |
| Çok yavaş ilk run | İlk derleyiş görüntüleri çeker ve derler — sonraki çalışmalar çok daha hızlı. Apple Silicon'da `linux/amd64` web görüntüsü emülasyonda çalışır. |
| Oturum açamıyorum | `.env` adresinde `OWNER_EMAIL` / `OWNER_PASSWORD` kontrol edin. İlk oturum açma parolayı değiştirmeye zorlar. |
| Yükseltmelerden sonra veritabanı tuhaf davranışı | `docker compose down -v` temiz bir başlangıç için birimi siler (yerel verileri kaybetmelidir). |

Hala takılı? [Bir Tartışma Açın](https://github.com/amusleh-spotware-com/cmind/discussions) — dostça. Sonraki durağı: [gerçekten dağıtmak →](./cloud.md)
