using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KargoApp.Shipments;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace KargoApp.Simulator;

// Okutma basmadan önce gerçek kargoları DB'ye oluşturur (ShipmentManager'ın kurallarıyla).
public class ShipmentSeeder : ITransientDependency
{
    private const int ChunkSize = 500;

    private readonly ShipmentManager _shipmentManager;
    private readonly IRepository<Shipment, Guid> _shipmentRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ShipmentSeeder(ShipmentManager shipmentManager,
                          IRepository<Shipment, Guid> shipmentRepository,
                          IUnitOfWorkManager unitOfWorkManager)
    {
        _shipmentManager = shipmentManager;
        _shipmentRepository = shipmentRepository;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task<List<string>> CreateAsync(int count, CancellationToken cancellationToken)
    {
        var trackingNumbers = new List<string>(count);

        for (var start = 0; start < count; start += ChunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // API'de UoW'u ABP her istek için açıyordu; burada HTTP isteği yok, kendimiz açıyoruz
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

            var shipments = new List<Shipment>();
            for (var i = start; i < Math.Min(start + ChunkSize, count); i++)
            {
                shipments.Add(await _shipmentManager.CreateAsync("Simülatör", $"Alıcı {i}", "Ankara", 1.5m));
            }

            await _shipmentRepository.InsertManyAsync(shipments, cancellationToken: cancellationToken);
            await uow.CompleteAsync(cancellationToken);   // commit

            trackingNumbers.AddRange(shipments.Select(s => s.TrackingNumber));
        }

        return trackingNumbers;
    }
}
