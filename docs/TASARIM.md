# Tasarım

[← README](../README.md) · [Deneyler ve ölçümler →](DENEYLER.md)

Bu dosya, KargoApp'teki ana tasarım kararlarını "sorun → çözüm → sonuç" şeklinde anlatır.

- [Domain: durum geçiş kuralları](#domain-durum-geçiş-kuralları)
- [1. Kendi inbox tablomuz](#1-mesajı-kaybetmeden-toplu-işlemek-kendi-inbox-tablomuz)
- [2. `FOR UPDATE SKIP LOCKED`](#2-paralel-ama-çakışmasız-for-update-skip-locked)
- [3. Exponential backoff](#3-sırasız-gelen-mesajlar-exponential-backoff)
- [4. Poison message ve DLQ](#4-poison-message-ve-dead-letter-queue)
- [5. Transactional outbox / inbox](#5-transactional-outbox--inbox-servisler-arası-kayıpsız-taşıma)
- [6. Redis shared counter](#6-shared-counter-redis-hincrby)

---

## Domain: durum geçiş kuralları

API ve Worker aynı Domain projesini kullanır; kargo durum geçiş kuralları tek yerde (`Shipment.ApplyMovement`) durur:

| Mevcut durum | İzin verilen okutma → yeni durum |
| --- | --- |
| `Created` | `DepartedBranch` → `InTransit` |
| `InTransit` | `ArrivedAtBranch` → `AtBranch` |
| `AtBranch` | `DepartedBranch` → `InTransit`, `OutForDelivery` → `OutForDelivery` |
| `OutForDelivery` | `Delivered` → `Delivered`, `DeliveryFailed` → `AtBranch` |
| `Delivered`, `Cancelled` | Hiçbiri (son durum) |

Bunun dışındaki her okutma `BusinessException` (`KargoAppDomainErrorCodes.InvalidShipmentStatusTransition`) ile reddedilir.

---

## 1. Mesajı kaybetmeden, toplu işlemek: kendi inbox tablomuz

ABP'nin hazır inbox'ı handler'ı her olay için **tek tek** çağırıyor ve bunu global bir distributed lock altında sırayla yapıyor. Handler okutmayı toplu işlemek için bir pakete koyup beklerse ABP olayı "işlendi" sayıyor; paket yazılamadan worker çökerse okutma kaybolur. Toplu ve paralel işleme ile kayıpsızlığı birlikte sağlamak için inbox elle kuruldu:

- `ScanEtoHandler` gelen okutmayı sadece `AppIncomingScans` tablosuna yazar; ABP sonra ack eder. Böylece RabbitMQ'dan mesaj, DB'ye kaydedilmeden asla silinmez.
- **Idempotency:** ScanId primary key. Aynı mesaj ikinci kez gelirse `UniqueViolation` yakalanır ve atlanır.
- Asıl işi sabit sayıda **lane** (`BackgroundService`) yapar: her lane DB'den bir paket kargo alır, tüm okutmalarını uygular ve tek transaction'da commit eder.

## 2. Paralel ama çakışmasız: `FOR UPDATE SKIP LOCKED`

```csharp
// ScanBatchHandler: EF Core FromSqlInterpolated, {…} değerleri SQL parametresi olarak gider
var shipments = await db.Shipments.FromSqlInterpolated($@"
    SELECT * FROM ""AppShipments""
    WHERE ""IsDeleted"" = false
      AND ""TrackingNumber"" IN (SELECT ""TrackingNumber"" FROM ""AppIncomingScans""
                                 WHERE ""ProcessedAt"" IS NULL
                                   AND (""NextAttemptAt"" IS NULL OR ""NextAttemptAt"" <= {now}))
    LIMIT {maxShipments}
    FOR UPDATE SKIP LOCKED")
    .IgnoreQueryFilters()
    .ToListAsync();
```

- Kilit **okutmaya değil kargoya** konur: aynı kargonun okutmaları aynı anda iki lane'de işlenemez.
- `SKIP LOCKED`: başkasının tuttuğu kargoyu beklemeden atla, boşta olanı al. Lane'ler ve birden çok worker kopyası birbirini hiç beklemez.
- Kilit transaction'la birlikte açılır; worker çökerse PostgreSQL kilidi anında bırakır. Kargo kilidi için Redis lock gerekmez. Projede Redis lock sadece ABP'nin outbox/inbox işleyicileri için kullanılıyor (bkz. [5](#5-transactional-outbox--inbox-servisler-arası-kayıpsız-taşıma)).

## 3. Sırasız gelen mesajlar: exponential backoff

RabbitMQ mesajları iki worker'a dağıtınca aynı kargonun okutmaları DB'ye farklı sırayla düşebiliyor. Örneğin `ArrivedAtBranch`, kendisinden önce olması gereken `DepartedBranch`'ten önce işlenmeye çalışılıyor ve geçiş kuralı onu reddediyor. İlk denemede 10.000 okutmanın **3.279'u** bu yüzden reddedildi.

- Geçersiz geçiş hemen hata sayılmaz: okutma **ertelenir** (2 → 4 → 8 → 16 sn), 5. denemede de geçersizse hatalı işaretlenir.
- Aynı kargonun paketteki sonraki okutmaları onunla birlikte bekler (deneme hakları yanmaz).
- Sonuç: **3.279 hata → 0**.

## 4. Poison message ve dead-letter queue

DB'ye hiç sığmayan bozuk bir mesaj, ABP hata durumunda her zaman `nack (requeue: true)` gönderdiği için **80 saniyede 265.000 kez** denendi. RabbitMQ 4.3'te `nack` quorum queue'nun `x-delivery-limit` sayacını da artırmıyor.

- Handler hatayı **sınıflandırır**: PostgreSQL'in kalıcı veri hataları (`22xxx`, unique dışındaki `23xxx`) tekrar denenmez. Mesaj, hata sebebiyle birlikte (`x-error` header) `KargoApp.DeadLetter` exchange'ine gönderilir.
- Geçici hatalar (DB'ye ulaşılamıyor gibi) eskisi gibi kuyruğa döner ve tekrar denenir.
- Sonuç: 265.000 deneme → 1 deneme + DLQ'da bekleyen mesaj.

## 5. Transactional outbox / inbox: servisler arası kayıpsız taşıma

Kargo `Delivered` olunca müşteriye bildirim gitmeli. "DB'ye yazdım ama RabbitMQ'ya gönderemeden çöktüm" (**dual-write** problemi) olmamalı.

```text
Worker (tek transaction)                         Notifier (ayrı servis, ayrı DB)
  okutmalar + kargo Delivered                      RabbitMQ → AbpEventInbox (MessageId unique) → ack
  + AbpEventOutbox'a olay        ── COMMIT         InboxProcessor → handler
OutboxSender → RabbitMQ  ─────────────────────►    hata: RetryLater (2, 4, 8… sn)
```

- `PublishAsync`, Unit of Work içindeyse olayı göndermez; commit'le birlikte outbox tablosuna yazar. Rollback olursa olay da yok olur.
- OutboxSender ve InboxProcessor birden çok kopyada çakışmasın diye **Redis distributed lock** kullanır.
- Notifier ayrı bir servis: sadece `Domain.Shared`'a (event sözleşmesine) bağlı, kendi kuyruğu ve kendi veritabanı var. Bildirim yavaşlarsa ya da çökerse okutma işleme etkilenmez.
- **Bilinen eksik:** `RetryLater` deneme hakkı bitince olayı discard ediyor; olay tabloda `Status = 1` olarak kalıyor, Aşama 5'teki gibi bir DLQ'ya gitmiyor.

## 6. Shared counter: Redis `HINCRBY`

Her worker kendi bellek içi sayacını tutuyordu; iki worker'ın toplamı görünmüyordu. Paket commit'ten sonra şube bazlı sayılar Redis'e `HINCRBY` ile yazılıyor. Tek komut atomik olduğu için iki worker aynı anda artırsa da sayım kaybolmuyor. Redis'e ulaşılamazsa paket bozulmuyor, sadece uyarı yazılıyor.
