---
title: "0006 - Kopyalama barindirma, atomik bir DB kira sozlesmesiyle koordine edilir"
description: Kopyalama profillerinin neden ayri bir koordinator yerine atomik Postgres kira sozlesmesi uzerinden talep edildigi ve bunun cift-kopyalamayi nasil onledigi.
---

# 0006 - Kopyalama barindirma, atomik bir DB kira sozlesmesiyle koordine edilir

## Baglam

Calisan bir kopyalama profilinin **tam olarak bir** node tarafindan barindirilmasi gerekir - iki ana makine ayni profilde = her kaynak islem ikili kez yansitilir (gercek para kaybedilir). Nodelar gelir ve gider (olcekme, cokmeler, sirali guncellemeler) ve ayri bir koordinator hizmeti calistirmak ve canli tutmak istemiyoruz.

## Karar

Her CopyEngineSupervisor, profilleri CopyProfiles tablosunda **atomik bir DB kira sozlesmesi** ile talep eder:

- **Talep** - atomik bir ExecuteUpdate (veya her node basina sinirlama yaparken FOR UPDATE SKIP LOCKED) atanmamis veya kira suresi dolmus profilleri alir. Atomiklik, iki yarisan supervisorin ayni satiri asla ikisi birden talep edememesi anlamina gelir.
- **Yenileme** - canli bir node her dongude kira sozlesmesini yeniler, boylece talebini korur.
- **Geri alma** - cokmus bir nodeun kira sozlesmesi suresi doldugunda, bir hayatta kalan bir sonraki dongusunde profili alir (oz-iyilesme). Duzgun kapatmada node kira sozlesmelerini **derhal serbest birakir** boylece failover hizlidir.
- **Izleme kopegi** - gorevi cikis yapmis ancak profil hâlâ bizim olan bir ana makine yeniden baslatilir.
- Uzlestirma, olcekte eszamanli UPDATE sel bastini onlemek için jittered (rastgele gecikme ile) yapilir.

## Sonuclar

- Dagitilacak veya saglikli tutulacak bagimsiz bir koordinator yok - PostgreSQL tek dogruluk kaynagidir.
- Cift kopyalama, uygulama duzeyinde kilitleme degil satir duzeyinde atomiklik ile onlenir.
- Failover gecmemesi, kira TTL'si ile sinirlidir (hizli-yol duzgun serbest birakma haric).
- Bu para yolu; DST (deterministic stress suite) tarafindan korunan yodur - bir DST senaryosunu gecirmek için zayiflatmayin.
