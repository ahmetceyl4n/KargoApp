using System;
using Volo.Abp.Domain.Entities;

namespace KargoApp.Shipments;

// Worker'ın aldığı ama henüz işlemediği okutma ("bekleyen okutmalar" rafı).
// Id = ScanId: aynı okutma iki kez gelirse veritabanı ikinciyi kabul etmez.
public class IncomingScan : BasicAggregateRoot<Guid>
{
    public string TrackingNumber { get; private set; } = default!;

    public string BranchCode { get; private set; } = default!;

    public MovementType MovementType { get; private set; }

    // Okutmanın şubede yapıldığı an
    public DateTime OccurredAt { get; private set; }

    // Worker'ın mesajı aldığı an
    public DateTime ReceivedAt { get; private set; }

    // null = hâlâ bekliyor
    public DateTime? ProcessedAt { get; private set; }

    // İşlenemediyse neden. ProcessedAt doluysa kalıcı hata, boşsa son ertelemenin sebebi.
    public string? Error { get; private set; }

    // Kaç kez denendi ve geçersiz çıktı
    public int AttemptCount { get; private set; }

    // En erken ne zaman tekrar denenebilir; null = hemen
    public DateTime? NextAttemptAt { get; private set; }

    protected IncomingScan()
    {
    }

    public IncomingScan(Guid scanId, string trackingNumber, string branchCode,
                        MovementType movementType, DateTime occurredAt, DateTime receivedAt)
        : base(scanId)
    {
        TrackingNumber = trackingNumber;
        BranchCode = branchCode;
        MovementType = movementType;
        OccurredAt = occurredAt;
        ReceivedAt = receivedAt;
    }

    public void MarkProcessed(DateTime processedAt)
    {
        Error = null;   // daha önce ertelendiyse son ertelemenin sebebi kalmasın
        ProcessedAt = processedAt;
    }

    public void MarkFailed(string error, DateTime processedAt)
    {
        Error = error.Truncate(IncomingScanConsts.MaxErrorLength);
        ProcessedAt = processedAt;
    }

    // Geçersiz geçiş: belki önceki okutma yolda. Deneme sayılır, sonra tekrar bakılır.
    // Bekleme süresini (2, 4, 8... sn) çağıran taraf hesaplar.
    public void Defer(string error, DateTime nextAttemptAt)
    {
        AttemptCount++;
        Error = error.Truncate(IncomingScanConsts.MaxErrorLength);
        NextAttemptAt = nextAttemptAt;
    }

    // Aynı kargonun önceki okutması ertelendi; bu okutma kendi sırasını bekliyor. Deneme sayılmaz.
    public void WaitUntil(DateTime nextAttemptAt)
    {
        NextAttemptAt = nextAttemptAt;
    }
}
