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

    // İşlenemediyse neden
    public string? Error { get; private set; }

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
        ProcessedAt = processedAt;
    }

    public void MarkFailed(string error, DateTime processedAt)
    {
        Error = error.Truncate(IncomingScanConsts.MaxErrorLength);
        ProcessedAt = processedAt;
    }
}
