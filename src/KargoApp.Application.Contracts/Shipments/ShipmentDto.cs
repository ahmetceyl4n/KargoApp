using System;
using Volo.Abp.Application.Dtos;

namespace KargoApp.Shipments;

public class ShipmentDto : AuditedEntityDto<Guid>
{
    public string TrackingNumber { get; set; } = default!;
    public string SenderName { get; set; } = default!;
    public string ReceiverName { get; set; } = default!;
    public string ReceiverCity { get; set; } = default!;
    public decimal WeightKg { get; set; }
    public ShipmentStatus Status { get; set; }
    public string? LastBranchCode { get; set; }
    public DateTime? LastMovementAt { get; set; }
}
