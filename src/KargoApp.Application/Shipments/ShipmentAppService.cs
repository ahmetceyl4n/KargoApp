using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace KargoApp.Shipments;

[Authorize]
public class ShipmentAppService : KargoAppAppService, IShipmentAppService
{
    private readonly IRepository<Shipment, Guid> _shipmentRepository;
    private readonly ShipmentManager _shipmentManager;

    public ShipmentAppService(IRepository<Shipment, Guid> shipmentRepository,
                              ShipmentManager shipmentManager)
    {
        _shipmentRepository = shipmentRepository;
        _shipmentManager = shipmentManager;
    }

    public async Task<ShipmentDto> CreateAsync(CreateShipmentDto input)
    {
        var shipment = await _shipmentManager.CreateAsync(
            input.SenderName, input.ReceiverName, input.ReceiverCity, input.WeightKg);

        // autoSave: CreationTime gibi audit alanları SaveChanges sırasında dolar; DTO'ya doğru değer gitsin.
        await _shipmentRepository.InsertAsync(shipment, autoSave: true);

        return ObjectMapper.Map<Shipment, ShipmentDto>(shipment);
    }

    public async Task<ShipmentDto> GetByTrackingNumberAsync(string trackingNumber)
    {
        var shipment = await GetShipmentAsync(trackingNumber);
        return ObjectMapper.Map<Shipment, ShipmentDto>(shipment);
    }

    public async Task<ShipmentDto> CancelAsync(string trackingNumber)
    {
        var shipment = await GetShipmentAsync(trackingNumber);
        shipment.Cancel();
        return ObjectMapper.Map<Shipment, ShipmentDto>(shipment);
    }

    private async Task<Shipment> GetShipmentAsync(string trackingNumber)
    {
        var shipment = await _shipmentRepository.FindAsync(s => s.TrackingNumber == trackingNumber);
        if (shipment == null)
        {
            throw new EntityNotFoundException(typeof(Shipment), trackingNumber);
        }
        return shipment;
    }
}
