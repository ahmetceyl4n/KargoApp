using KargoApp.Shipments;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace KargoApp;

// Target: Sadece DTO'daki her alanın doldurulduğunu kontrol et.
// Entity'de olup DTO'da olmayan alanlar (IsDeleted, ConcurrencyStamp...) bilerek dışarıda.
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ShipmentToShipmentDtoMapper : MapperBase<Shipment, ShipmentDto>
{
    public override partial ShipmentDto Map(Shipment source);

    public override partial void Map(Shipment source, ShipmentDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ShipmentMovementToShipmentMovementDtoMapper : MapperBase<ShipmentMovement, ShipmentMovementDto>
{
    public override partial ShipmentMovementDto Map(ShipmentMovement source);

    public override partial void Map(ShipmentMovement source, ShipmentMovementDto destination);
}
