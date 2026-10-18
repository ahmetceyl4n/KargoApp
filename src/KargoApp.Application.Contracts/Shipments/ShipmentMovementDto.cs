using System;
using Volo.Abp.Application.Dtos;

namespace KargoApp.Shipments;

public class ShipmentMovementDto : CreationAuditedEntityDto<Guid>
{
    public Guid ScanId { get; set; }
    public string BranchCode { get; set; } = default!;
    public MovementType MovementType { get; set; }
    public DateTime OccurredAt { get; set; }
}
