using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using KargoApp.EntityFrameworkCore;
using KargoApp.Shipments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace KargoApp.Worker;

// Bir paketi baştan sona işler: kargoları kilitle, okutmaları uygula, toplu kaydet.
public class ScanBatchHandler : ITransientDependency
{
    private readonly IDbContextProvider<KargoAppDbContext> _dbContextProvider;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ShipmentManager _shipmentManager;
    private readonly ScanStats _stats;
    private readonly IClock _clock;
    private readonly ILogger<ScanBatchHandler> _logger;

    public ScanBatchHandler(IDbContextProvider<KargoAppDbContext> dbContextProvider,
                            IUnitOfWorkManager unitOfWorkManager,
                            ShipmentManager shipmentManager,
                            ScanStats stats,
                            IClock clock,
                            ILogger<ScanBatchHandler> logger)
    {
        _dbContextProvider = dbContextProvider;
        _unitOfWorkManager = unitOfWorkManager;
        _shipmentManager = shipmentManager;
        _stats = stats;
        _clock = clock;
        _logger = logger;
    }

    // İşlenen okutma sayısını döner; 0 = iş yok
    public async Task<int> ProcessNextBatchAsync(int lane, int maxShipments)
    {
        var stopwatch = Stopwatch.StartNew();

        // a) Transaction aç: paket ya hep ya hiç
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var db = await _dbContextProvider.GetDbContextAsync();

        // b) Bekleyen okutması olan, başkasının tutmadığı en fazla K kargoyu kilitle.
        //    SKIP LOCKED LINQ ile yazılamaz; soft-delete filtresini kapatıp koşulu SQL'e kendimiz yazıyoruz.
        var shipments = await db.Shipments
            .FromSqlInterpolated($@"
                SELECT * FROM ""AppShipments""
                WHERE ""IsDeleted"" = false
                  AND ""TrackingNumber"" IN (SELECT ""TrackingNumber"" FROM ""AppIncomingScans""
                                             WHERE ""ProcessedAt"" IS NULL)
                LIMIT {maxShipments}
                FOR UPDATE SKIP LOCKED")
            .IgnoreQueryFilters()
            .ToListAsync();

        if (shipments.Count == 0)
        {
            return 0;
        }

        // c) Bu kargoların bekleyen okutmaları, tarih sırasıyla
        var trackingNumbers = shipments.Select(s => s.TrackingNumber).ToList();
        var scans = await db.IncomingScans
            .Where(x => x.ProcessedAt == null && trackingNumbers.Contains(x.TrackingNumber))
            .OrderBy(x => x.OccurredAt)
            .ToListAsync();

        // Toplu kontroller: okutma başına sorgu yerine tek sorgu
        var branchCodes = await db.Branches.Select(b => b.Id).ToHashSetAsync();
        var scanIds = scans.Select(x => x.Id).ToList();
        var alreadyApplied = await db.ShipmentMovements
            .Where(m => scanIds.Contains(m.ScanId))
            .Select(m => m.ScanId)
            .ToHashSetAsync();

        // d) Kuralı uygula; hatalı okutma paketi düşürmez
        var shipmentsByTracking = shipments.ToDictionary(s => s.TrackingNumber);
        var movements = new List<ShipmentMovement>();
        var failed = 0;
        var now = _clock.Now;

        foreach (var scan in scans)
        {
            if (alreadyApplied.Contains(scan.Id))
            {
                scan.MarkProcessed(now);   // daha önce uygulanmış (ör. API'den), sessizce geç
                continue;
            }

            try
            {
                if (!branchCodes.Contains(scan.BranchCode))
                {
                    throw new BusinessException(KargoAppDomainErrorCodes.BranchNotFound);
                }

                movements.Add(_shipmentManager.ApplyMovement(shipmentsByTracking[scan.TrackingNumber],
                    scan.Id, scan.BranchCode, scan.MovementType, scan.OccurredAt));
                scan.MarkProcessed(now);
            }
            catch (BusinessException ex)
            {
                scan.MarkFailed(ex.Code!, now);
                failed++;
                _logger.LogWarning("Okutma atlandı {ScanId} {TrackingNumber} {MovementType}: {Error}",
                    scan.Id, scan.TrackingNumber, scan.MovementType, ex.Code);
            }
        }

        // e) Toplu kaydet ve commit → kilitler açılır
        await db.ShipmentMovements.AddRangeAsync(movements);
        await uow.CompleteAsync();

        // Sadece commit başarılıysa say: paket geri alınsaydı buraya gelinmezdi
        foreach (var movement in movements)
        {
            _stats.RecordProcessed(movement.BranchCode);
        }
        _stats.RecordFailed(failed);

        _logger.LogInformation("Şerit {Lane}: {Shipments} kargo, {Scans} okutma ({Failed} hatalı), {Ms} ms",
            lane, shipments.Count, scans.Count, failed, stopwatch.ElapsedMilliseconds);

        return scans.Count;
    }
}
