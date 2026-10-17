using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace KargoApp.Shipments;

public class ShipmentManager : DomainService
{
    private readonly IRepository<Shipment, Guid> _shipmentRepository;

    public ShipmentManager(IRepository<Shipment, Guid> shipmentRepository)
    {
        _shipmentRepository = shipmentRepository;
    }

    public async Task<Shipment> CreateAsync(string senderName, string receiverName,
                                            string receiverCity, decimal weightKg)
    {
        var trackingNumber = await GenerateUniqueTrackingNumberAsync();
        return new Shipment(GuidGenerator.Create(), trackingNumber,
                            senderName, receiverName, receiverCity, weightKg);
    }

    private async Task<string> GenerateUniqueTrackingNumberAsync()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var digits = Random.Shared.NextInt64(0, 10_000_000_000)
                               .ToString($"D{ShipmentConsts.TrackingNumberDigitCount}");
            var trackingNumber = ShipmentConsts.TrackingNumberPrefix + digits;

            if (await _shipmentRepository.FindAsync(s => s.TrackingNumber == trackingNumber) == null)
            {
                return trackingNumber;
            }
        }

        throw new AbpException("Benzersiz takip numarası üretilemedi.");
    }
}
