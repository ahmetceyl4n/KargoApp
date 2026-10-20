using System;
using Volo.Abp.EventBus;

namespace KargoApp.Shipments;

// Şube okuyucusunun bir okutması. Simülatör yayınlar, Worker işler.
// Eto = Event Transfer Object: entity değil, kabloda giden düz veri.
[EventName("KargoApp.Scan")]
public class ScanEto
{
    public Guid ScanId { get; set; }

    public string TrackingNumber { get; set; } = default!;

    public string BranchCode { get; set; } = default!;

    public MovementType MovementType { get; set; }

    public DateTime OccurredAt { get; set; }
}
