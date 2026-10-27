using System;
using Volo.Abp.EventBus;

namespace KargoApp.Shipments;

// Kargo teslim edildi. Worker yayınlar (outbox ile), Notifier dinler (inbox ile).
[EventName("KargoApp.ShipmentDelivered")]
public class ShipmentDeliveredEto
{
    public Guid ShipmentId { get; set; }

    public string TrackingNumber { get; set; } = default!;

    public string BranchCode { get; set; } = default!;   // teslimatı yapan şube

    public DateTime DeliveredAt { get; set; }
}
