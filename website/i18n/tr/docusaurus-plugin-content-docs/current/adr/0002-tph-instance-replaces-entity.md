---
title: "ADR-0002: TPH örneği, varlığı değiştirir"
---

# ADR-0002: TPH örneği, varlığı değiştirir

## Bağlam

Örnek bir yaşam döngüsü var: başlangıç → çalışılıyor → terminal (başarılı/başarısız). Durum
değiştikçe davranış değişir.

## Karar

Örnek durum **TPH (Tablo-ilk Hiyerarşi)** kullanılır; bir geçiş **yeni varlığın yerine geçer**:

- `Instance.Id` → yeni örnek kimliği (başlangıç → çalıştırılıyor)
- `Instance.Id` → yeni örnek kimliği (çalıştırılıyor → başarısız)

Konteyner kimliği kararlı — HTTP aracısı bunu takip eder; örnek kimliği değişir.

## Sonuçlar

- Hiçbir `if (state == ...)` mutable entity karmaşaklığı.
- EF sahneleme basit: biz yeni varlığı kaydetmek.
- İstemciler durum geçişlerini izlemek için veritabanını yoklayabilir.
