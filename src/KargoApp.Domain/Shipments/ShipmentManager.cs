using System;
using System.Threading.Tasks;
using KargoApp.Branches;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace KargoApp.Shipments;

public class ShipmentManager : DomainService
{
    private readonly IRepository<Shipment, Guid> _shipmentRepository;
    private readonly IRepository<ShipmentMovement, Guid> _movementRepository;
    private readonly IRepository<Branch, string> _branchRepository;

    public ShipmentManager(IRepository<Shipment, Guid> shipmentRepository,
                           IRepository<ShipmentMovement, Guid> movementRepository,
                           IRepository<Branch, string> branchRepository)
    {
        _shipmentRepository = shipmentRepository;
        _movementRepository = movementRepository;
        _branchRepository = branchRepository;
    }

    public async Task<Shipment> CreateAsync(string senderName, string receiverName,
                                            string receiverCity, decimal weightKg)
    {
        var trackingNumber = await GenerateUniqueTrackingNumberAsync();
        return new Shipment(GuidGenerator.Create(), trackingNumber,
                            senderName, receiverName, receiverCity, weightKg);
    }

    public async Task<ShipmentMovement> AddMovementAsync(Shipment shipment, Guid scanId,
        string branchCode, MovementType movementType, DateTime occurredAt)
    {
        // 1. Aynı okutma daha önce geldi mi? (idempotency)
        if (await _movementRepository.FindAsync(m => m.ScanId == scanId) != null)
        {
            throw new BusinessException(KargoAppDomainErrorCodes.DuplicateScan)
                .WithData("ScanId", scanId);
        }

        // 2. Şube var mı?
        if (await _branchRepository.FindAsync(branchCode) == null)
        {
            throw new BusinessException(KargoAppDomainErrorCodes.BranchNotFound)
                .WithData("BranchCode", branchCode);
        }

        // 3 + 4. Kural ve hareket kaydı
        return ApplyMovement(shipment, scanId, branchCode, movementType, occurredAt);
    }

    // Toplu işleme (worker) için: DB kontrolleri (tekrar okutma, şube) çağıran tarafta toplu yapılır.
    public ShipmentMovement ApplyMovement(Shipment shipment, Guid scanId,
        string branchCode, MovementType movementType, DateTime occurredAt)
    {
        // 3. Kargonun kendi kuralı
        shipment.ApplyMovement(movementType, branchCode, occurredAt);

        // 4. Hareket kaydı (internal constructor'ı sadece Domain çağırabilir)
        return new ShipmentMovement(GuidGenerator.Create(), shipment.Id, scanId,
                                    branchCode, movementType, occurredAt);
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
