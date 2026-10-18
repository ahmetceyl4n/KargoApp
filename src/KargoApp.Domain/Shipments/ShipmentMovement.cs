using System;
using KargoApp.Branches;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace KargoApp.Shipments;

public class ShipmentMovement : CreationAuditedEntity<Guid>
{
    public Guid ShipmentId { get; private set; }
    public Guid ScanId { get; private set; }
    public string BranchCode { get; private set; } = default!;
    public MovementType MovementType { get; private set; }
    public DateTime OccurredAt { get; private set; }

    // EF Core veritabanından okurken kullanır.
    protected ShipmentMovement() { }

    // Sadece ShipmentManager oluşturabilir (kurallar kontrol edildikten sonra).
    internal ShipmentMovement(Guid id, Guid shipmentId, Guid scanId, string branchCode,
                              MovementType movementType, DateTime occurredAt)
        : base(id)
    {
        ShipmentId = shipmentId;
        ScanId = scanId;
        BranchCode = Check.NotNullOrWhiteSpace(branchCode, nameof(branchCode), BranchConsts.MaxCodeLength);
        MovementType = movementType;
        OccurredAt = occurredAt;
    }
}
