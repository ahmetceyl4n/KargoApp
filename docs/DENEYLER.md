# Deneyler ve ölçümler

[← README](../README.md) · [Tasarım →](TASARIM.md)

Tasarım kararları sadece yazılıp bırakılmadı, bilerek bozularak denendi. Bu dosya ölçümleri ve dayanıklılık deneylerinin sonuçlarını içerir.

## Publish paralelliği (Simulator, 10.000 okutma)

| Paralellik | Saniyede | Not |
| --- | --- | --- |
| 1 | ~20 | Her publish ~50 ms bekliyor |
| 10 | ~2.250 | Beklemeler üst üste biniyor: async'in asıl gücü |
| 100 | ilk ~60 sn **0**, sonra ~3.000 | **Thread pool starvation** |

Paralellik 100'de ilerleme ilk dakika tamamen durdu. Gönderim yolunda bir yer thread'i bloklayınca havuzdaki thread'ler tükeniyor; .NET yeni thread'i saniyede ~1-2 tane ekliyor. `DOTNET_ThreadPool_ForceMinWorkerThreads` ile havuz baştan büyütülünce donma kayboldu ve teşhis doğrulandı.

**Ders:** "async" görünen kodun altında da bloklayan bir şey olabilir. Paralellik arttıkça hız sonsuza gitmez; bir yerden sonra darboğaz başka yere kayar.

## İşleme hızı (Worker, 4.000 okutma)

Okutmalar önce lane'siz bir worker ile (`--ScanProcessing:Lanes=0`) inbox tablosuna dolduruldu, sonra sadece tablonun boşalma süresi ölçüldü. Böylece alma hızı ölçüme karışmadı.

| Lane | Paket (kargo) | Saniyede | Not |
| --- | --- | --- | --- |
| 1 | 1 | ~320 | Her kargo için ayrı transaction |
| 1 | 50 | ~1.600 | Batching tek başına 5×: sabit paket maliyeti 1.000 yerine 20 kez ödendi |
| 4 | 50 | ~4.500 | 4× değil ~2,8×: lane'ler aynı DB için yarışıyor; paralellik doğrusal ölçeklenmez |

**Ölçüm tuzağı:** RabbitMQ yönetim arayüzünün istatistikleri birkaç saniye gecikmeli. İlk denemede kuyruk "boş" sanıldı ve sonuçlar anlamsız çıktı. Doğrusu, kesin kaynağa (DB satır sayısı) bakmak.

## Dayanıklılık deneyleri

| Deney | Sonuç |
| --- | --- |
| Queue yokken publish | Mesajlar kayboldu (exchange bırakacak yer bulamadı) |
| Worker kapalıyken 10.000 okutma | Queue'da bekledi, worker açılınca işlendi |
| Worker'ı işlem ortasında öldürmek | Kayıp yok; tekrar gelen 1 mesajı idempotency yakaladı |
| İki worker, 10.000 okutma | Backoff öncesi 3.279 hata, sonrası **0** |
| Poison message | 265.000 deneme → 1 deneme + DLQ |
| Notifier kapalıyken 50 teslimat | Kaybolmadı, açılınca işlendi |
| RabbitMQ kapalıyken 50 teslimat | Outbox'ta bekledi; RabbitMQ açılınca 6 sn içinde gitti |
| İki worker, 100 teslimat | 100 olay, 100 farklı kargo; çift bildirim yok |
| Bildirimde %20 hata (`--Notifier:FailurePercent=20`) | Retry ile geçti (1 hata → 2-3 sn, 3 hata → 14-15 sn); 6 kez üst üste hata alan 1 olay **discard** edildi |

### Backoff sonrası deneme dağılımı (iki worker, 10.000 okutma)

| Deneme | Okutma sayısı |
| --- | --- |
| 1. denemede işlendi | 8.612 |
| 2. denemede | 937 |
| 3. denemede | 340 |
| 4. denemede | 109 |
| 5. denemede | 2 |
