using System;
using System.Collections.Generic;
using System.Linq;
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

    // Geçiş tablosu (PLAN.md → "Durumlar ve geçerli geçişler"):
    // Kargo bu durumdayken şu hareketler kabul edilir.
    private static readonly Dictionary<ShipmentStatus, MovementType[]> AllowedMovements = new()
    {
        [ShipmentStatus.Created]        = new[] { MovementType.DepartedBranch },
        [ShipmentStatus.InTransit]      = new[] { MovementType.ArrivedAtBranch },
        [ShipmentStatus.AtBranch]       = new[] { MovementType.DepartedBranch, MovementType.OutForDelivery },
        [ShipmentStatus.OutForDelivery] = new[] { MovementType.Delivered, MovementType.DeliveryFailed },
        [ShipmentStatus.Delivered]      = Array.Empty<MovementType>(),
        [ShipmentStatus.Cancelled]      = Array.Empty<MovementType>(),
    };

    public void ApplyMovement(MovementType movementType, string branchCode, DateTime occurredAt)
    {
        // 1. Bu durumdayken bu hareket kabul ediliyor mu?
        if (!AllowedMovements[Status].Contains(movementType))
        {
            throw new BusinessException(KargoAppDomainErrorCodes.InvalidShipmentStatusTransition)
                .WithData("CurrentStatus", Status)
                .WithData("MovementType", movementType);
        }

        // 2. Hareketin götürdüğü yeni durum
        Status = movementType switch
        {
            MovementType.DepartedBranch  => ShipmentStatus.InTransit,
            MovementType.ArrivedAtBranch => ShipmentStatus.AtBranch,
            MovementType.OutForDelivery  => ShipmentStatus.OutForDelivery,
            MovementType.Delivered       => ShipmentStatus.Delivered,
            MovementType.DeliveryFailed  => ShipmentStatus.AtBranch,
            _ => throw new ArgumentOutOfRangeException(nameof(movementType))
        };

        // 3. Son konum ve zaman
        LastBranchCode = branchCode;
        LastMovementAt = occurredAt;
    }
}
