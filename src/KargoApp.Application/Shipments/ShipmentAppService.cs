using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace KargoApp.Shipments;

[Authorize]
public class ShipmentAppService : KargoAppAppService, IShipmentAppService
{
    private readonly IRepository<Shipment, Guid> _shipmentRepository;
    private readonly ShipmentManager _shipmentManager;
    private readonly IRepository<ShipmentMovement, Guid> _shipmentMovementRepository;
    private readonly IDistributedCache<ShipmentDto, string> _shipmentCache;

    public ShipmentAppService(IRepository<Shipment, Guid> shipmentRepository,
                              ShipmentManager shipmentManager,
                              IRepository<ShipmentMovement, Guid> shipmentMovementRepository,
                              IDistributedCache<ShipmentDto, string> shipmentCache)
    {
        _shipmentRepository = shipmentRepository;
        _shipmentManager = shipmentManager;
        _shipmentMovementRepository = shipmentMovementRepository;
        _shipmentCache = shipmentCache;
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
        // Cache-aside: Redis'te varsa oradan, yoksa veritabanından oku ve 5 dk cache'le.
        return (await _shipmentCache.GetOrAddAsync(
            trackingNumber,
            async () =>
            {
                var shipment = await GetShipmentAsync(trackingNumber);
                return ObjectMapper.Map<Shipment, ShipmentDto>(shipment);
            },
            () => new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            }))!;
    }

    public async Task<ShipmentDto> CancelAsync(string trackingNumber)
    {
        var shipment = await GetShipmentAsync(trackingNumber);
        shipment.Cancel();

        // considerUow: commit'ten SONRA sil; araya giren bir sorgu eski veriyi cache'e geri koyamasın.
        await _shipmentCache.RemoveAsync(trackingNumber, considerUow: true);

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

    public async Task<ShipmentDto> AddMovementAsync(string trackingNumber, AddMovementDto input)
    {
        var shipment = await GetShipmentAsync(trackingNumber);

        // Kurallar Domain'de: ScanId, şube, geçiş tablosu
        var movement = await _shipmentManager.AddMovementAsync(
            shipment, input.ScanId!.Value, input.BranchCode,
            input.MovementType!.Value, input.OccurredAt!.Value);

        await _shipmentMovementRepository.InsertAsync(movement);

        // Hareket + kargodaki değişiklik tek seferde; LastModificationTime DTO'ya doğru gitsin.
        await CurrentUnitOfWork!.SaveChangesAsync();

        await _shipmentCache.RemoveAsync(trackingNumber, considerUow: true);

        return ObjectMapper.Map<Shipment, ShipmentDto>(shipment);
    }

    public async Task<ListResultDto<ShipmentMovementDto>> GetHistoryAsync(string trackingNumber)
    {
        var shipment = await GetShipmentAsync(trackingNumber);

        var queryable = await _shipmentMovementRepository.GetQueryableAsync();
        var movements = await AsyncExecuter.ToListAsync(
            queryable.Where(m => m.ShipmentId == shipment.Id)
                     .OrderBy(m => m.OccurredAt));

        return new ListResultDto<ShipmentMovementDto>(
            movements.Select(m => ObjectMapper.Map<ShipmentMovement, ShipmentMovementDto>(m)).ToList());
    }
}
