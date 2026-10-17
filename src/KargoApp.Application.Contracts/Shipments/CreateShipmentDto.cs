using System.ComponentModel.DataAnnotations;

namespace KargoApp.Shipments;

public class CreateShipmentDto
{
    [Required]
    [StringLength(ShipmentConsts.MaxNameLength)]
    public string SenderName { get; set; } = default!;

    [Required]
    [StringLength(ShipmentConsts.MaxNameLength)]
    public string ReceiverName { get; set; } = default!;

    [Required]
    [StringLength(ShipmentConsts.MaxCityLength)]
    public string ReceiverCity { get; set; } = default!;

    [Range(ShipmentConsts.MinWeightKg, ShipmentConsts.MaxWeightKg)]
    public decimal WeightKg { get; set; }
}
