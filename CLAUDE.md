# KargoApp

Öğrenme projesi: ABP, worker, eşzamanlılık, RabbitMQ (MassTransit v8) ve Redis. Amaç ürün değil, her aşamada bir konuyu kavramak.

**Her oturumda önce [PLAN.md](PLAN.md) oku.** Özellikle "Kapsam ve sınırlar" ve "Çalışma kuralları (Claude Code için)" bölümleri bağlayıcıdır.

## Kısa kurallar

- Sadece kullanıcının söylediği aşamayı yap; sonraki aşamaya kendiliğinden geçme.
- Kod yazmadan önce adımları madde madde yaz, onay bekle.
- Her değişiklikte neyi neden yaptığını 1–2 cümleyle açıkla; ABP / MassTransit'in arka planda yaptığını belirt.
- Domain kurallarını (`Shipment.Cancel`, `Shipment.ApplyMovement`, geçiş tablosu) kullanıcı yazar: iskelet kur, `// TODO (kullanıcı): ...` ile işaretle, sonra review et.
- Kapsam dışı bir şey gerekirse ekleme; kullanıcıya söyle ve `SONRA.md`'ye not düş.
- Her aşama `main`'den ayrı branch (`asama-N-...`), commit mesajları `asama-N: ...` önekli.
- "Bitti sayılır eğer" maddelerini kullanıcıya elle nasıl deneyeceğini anlat; kendin "hepsi geçti" deme.
- "Kendine sor" sorularını cevaplama; kullanıcı `NOTLAR.md`'ye yazar, sen kontrol edersin.
- Kullanıcıyla Türkçe konuş.

## Teknik seçimler

- Veritabanı: PostgreSQL (Docker)
- ABP katmanlı şablon, UI yok
- Altyapı: `docker-compose.yml` (PostgreSQL + RabbitMQ + Redis)
