---
title: TPH; Geçiş, Varlığı Değiştirir
---

# Örnek Durum TPH'dir; Bir Geçiş Varlığı Değiştirir

**Bağlam:** cMind örneğinin (cBot çalışması, backtest) yaşam döngüsü: Başlama → Çalışıyor → Terminal (Durduruldu/Başarısız). Durum hangisine bağlı olduğu, ne için devredilip ne yapabileceği değişir.

**Karar:** Tablo başına Hiyerarşi (TPH) ayrımcılığı ile; durum **geçiş sonunda varlık değiştirildiğinde.**

- `RunningInstance`, `BacktestInstance` vs. ayrı TPH davranış türleri.
- Durum değişikliği: eski varlık kalır, yeni geçiş durumu için yeni yazı derlenmiş (id'si değişir).
- Konteyner id ve yürütme raporları taşınır, örnek id değiş.

**Sonuçlar:**

✅ **Tür güvenliği:** Derleyici geçiş sırasında geçersiz uç nokta çağrılarını yakalar.

✅ **Durum makinesi güvenliği:** `RunningInstance` üzerindeki "başlama" durumu türü tarafından yok sayılır.

❌ **Şema karmaşıklığı:** Benzer veriler çok tablosu arasında yaşadığında, nerede sorgu yazarsınız?

İlgili: [0003-external-nodes-http-jwt →](./0003-external-nodes-http-jwt.md)
