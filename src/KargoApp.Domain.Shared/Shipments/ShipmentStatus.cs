namespace KargoApp.Shipments;

public enum ShipmentStatus
{
    Created = 0,
    InTransit = 1,
    AtBranch = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Cancelled = 5
}
