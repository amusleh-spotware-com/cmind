---
title: "ADR-0004: CBotBuilder web sunucusunda"
---

# ADR-0004: CBotBuilder web sunucusunda

## Bağlam

cBot derleme untrusted MSBuild çalıştırır — kullanıcı kodu, harici paketler, yol tarama.

## Karar

`CBotBuilder` **web sunucusu üzerinde çalışır** (Docker soketine erişim gerekir) bir tek seferlik SDK
konteynerinde:

```bash
docker run -it --rm -v /work:/work -v app-nuget-cache:/root/.nuget \
  mcr.microsoft.com/dotnet/sdk:10 \
  dotnet build /work/cBot.csproj
```

- Bind-mount `/work` (kullanıcı projesine)
- Paylaşılmış `app-nuget-cache` hacmi (paket indirmeyi hızlandırır)
- Konteyner, ana bilgisayar FS/net erişemez (güvenlik sınırı)

## Sonuçlar

- MSBuild malicious komutlar kesinlikle sınırlandırılmış.
- Derleme yalıtılmış; birkaç saniye sonra başında silinir.
- Ölçek: çoklu derlemeler yapılamaz — web sunucusunda DinD ve kuyruk gereklidir.
