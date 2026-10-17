using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace KargoApp.Shipments;

public class Shipment : FullAuditedAggregateRoot<Guid>
{
    public string TrackingNumber { get; private set; } = default!;
    public string SenderName { get; private set; } = default!;
    public string ReceiverName { get; private set; } = default!;
    public string ReceiverCity { get; private set; } = default!;
    public decimal WeightKg { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public string? LastBranchCode { get; private set; }
    public DateTime? LastMovementAt { get; private set; }

    // EF Core veritabanından okurken nesneyi bu constructor ile oluşturur.
    protected Shipment() { }

    // internal: Bu constructor'ı sadece Domain projesi (yani ShipmentManager) çağırabilir.
    internal Shipment(Guid id, string trackingNumber, string senderName,
                      string receiverName, string receiverCity, decimal weightKg)
        : base(id)
    {
        TrackingNumber = Check.NotNullOrWhiteSpace(trackingNumber, nameof(trackingNumber), ShipmentConsts.TrackingNumberLength);
        SenderName = Check.NotNullOrWhiteSpace(senderName, nameof(senderName), ShipmentConsts.MaxNameLength);
        ReceiverName = Check.NotNullOrWhiteSpace(receiverName, nameof(receiverName), ShipmentConsts.MaxNameLength);
        ReceiverCity = Check.NotNullOrWhiteSpace(receiverCity, nameof(receiverCity), ShipmentConsts.MaxCityLength);
        WeightKg = Check.Range(weightKg, nameof(weightKg),
            (decimal)ShipmentConsts.MinWeightKg, (decimal)ShipmentConsts.MaxWeightKg);
        Status = ShipmentStatus.Created;
    }

    public void Cancel()
    {
        if (Status != ShipmentStatus.Created)
        {
            throw new BusinessException(KargoAppDomainErrorCodes.ShipmentCannotBeCancelled)
                .WithData("CurrentStatus", Status);
        }

        Status = ShipmentStatus.Cancelled;
    }
}
