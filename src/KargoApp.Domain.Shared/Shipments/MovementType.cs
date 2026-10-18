namespace KargoApp.Shipments;

public enum MovementType
{
    DepartedBranch = 0,   // Şubeden çıkış       → InTransit
    ArrivedAtBranch = 1,  // Şubeye varış        → AtBranch
    OutForDelivery = 2,   // Dağıtıma çıkış      → OutForDelivery
    Delivered = 3,        // Teslim edildi       → Delivered
    DeliveryFailed = 4    // Teslim edilemedi    → AtBranch
}
