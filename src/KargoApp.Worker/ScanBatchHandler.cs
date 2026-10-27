using KargoApp.EntityFrameworkCore;
using KargoApp.Shipments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EventBus.Distributed;
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
    private readonly SharedScanCounter _sharedCounter;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IClock _clock;
    private readonly ILogger<ScanBatchHandler> _logger;
    private readonly ScanProcessingOptions _options;

    public ScanBatchHandler(IDbContextProvider<KargoAppDbContext> dbContextProvider,
                            IUnitOfWorkManager unitOfWorkManager,
                            ShipmentManager shipmentManager,
                            ScanStats stats,
                            SharedScanCounter sharedCounter,
                            IDistributedEventBus distributedEventBus,
                            IClock clock,
                            ILogger<ScanBatchHandler> logger,
                            IOptions<ScanProcessingOptions> options)
    {
        _dbContextProvider = dbContextProvider;
        _unitOfWorkManager = unitOfWorkManager;
        _shipmentManager = shipmentManager;
        _stats = stats;
        _sharedCounter = sharedCounter;
        _distributedEventBus = distributedEventBus;
        _clock = clock;
        _logger = logger;
        _options = options.Value;
    }

    // İşlenen okutma sayısını döner; 0 = iş yok
    public async Task<int> ProcessNextBatchAsync(int lane, int maxShipments)
    {
        var now = _clock.Now;
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
                                             WHERE ""ProcessedAt"" IS NULL
                                               AND (""NextAttemptAt"" IS NULL OR ""NextAttemptAt"" <= {now}))
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
        var deliveredEvents = new List<ShipmentDeliveredEto>();
        var blocked = new Dictionary<string, DateTime>();   // bu pakette takılan kargo → ne zamana kadar beklesin
        var deferred = 0;
        var failed = 0;

        foreach (var scan in scans)
        {
            if (alreadyApplied.Contains(scan.Id))
            {
                scan.MarkProcessed(now);   // daha önce uygulanmış (ör. API'den), sessizce geç
                continue;
            }

            // Bu kargonun önceki okutması bu pakette ertelendiyse, bu okutma onunla birlikte beklesin
            // (denenmez, deneme hakkı da yanmaz)
            if (blocked.TryGetValue(scan.TrackingNumber, out var waitUntil))
            {
                scan.WaitUntil(waitUntil);
                continue;
            }

            try
            {
                if (!branchCodes.Contains(scan.BranchCode))
                {
                    throw new BusinessException(KargoAppDomainErrorCodes.BranchNotFound);
                }

                var shipment = shipmentsByTracking[scan.TrackingNumber];
                movements.Add(_shipmentManager.ApplyMovement(shipment,
                    scan.Id, scan.BranchCode, scan.MovementType, scan.OccurredAt));
                scan.MarkProcessed(now);

                // Teslim edildiyse bildirim olayı: commit'le birlikte outbox'a yazılacak
                if (scan.MovementType == MovementType.Delivered)
                {
                    deliveredEvents.Add(new ShipmentDeliveredEto
                    {
                        ShipmentId = shipment.Id,
                        TrackingNumber = shipment.TrackingNumber,
                        BranchCode = scan.BranchCode,
                        DeliveredAt = scan.OccurredAt
                    });
                }
            }
            // Geçersiz geçiş ve hâlâ hakkı var → ertele: belki önceki okutma yolda (2, 4, 8, 16 sn)
            catch (BusinessException ex) when (ex.Code == KargoAppDomainErrorCodes.InvalidShipmentStatusTransition
                                               && scan.AttemptCount + 1 < _options.MaxAttempts)
            {
                var nextAttemptAt = now.AddMilliseconds(_options.RetryDelayMs * Math.Pow(2, scan.AttemptCount));
                scan.Defer(ex.Code, nextAttemptAt);
                blocked[scan.TrackingNumber] = nextAttemptAt;
                deferred++;
            }
            // Kalıcı hata (şube yok) ya da deneme hakkı bitti
            catch (BusinessException ex)
            {
                scan.MarkFailed(ex.Code!, now);
                failed++;
                _logger.LogWarning("Okutma atlandı {ScanId} {TrackingNumber} {MovementType} ({Attempt}. deneme): {Error}",
                    scan.Id, scan.TrackingNumber, scan.MovementType, scan.AttemptCount + 1, ex.Code);
            }
        }

        // e) Toplu kaydet ve commit → kilitler açılır
        await db.ShipmentMovements.AddRangeAsync(movements);

        // Olaylar RabbitMQ'ya gitmez: UoW'a eklenir, CompleteAsync aynı transaction'da AbpEventOutbox'a yazar
        foreach (var eto in deliveredEvents)
        {
            await _distributedEventBus.PublishAsync(eto);
        }

        await uow.CompleteAsync();

        // Sadece commit başarılıysa say: paket geri alınsaydı buraya gelinmezdi
        foreach (var movement in movements)
        {
            _stats.RecordProcessed(movement.BranchCode);
        }
        _stats.RecordFailed(failed);
        _stats.RecordDeferred(deferred);

        // Ortak defter (bütün worker'lar). Sayaç ikincil bilgi: Redis'e ulaşılamazsa paketi bozma, sadece uyar.
        try
        {
            await _sharedCounter.RecordAsync(movements.Select(m => m.BranchCode), failed, deferred);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ortak sayaç güncellenemedi (Redis)");
        }

        _logger.LogInformation("Şerit {Lane}: {Shipments} kargo, {Scans} okutma ({Deferred} ertelendi, {Failed} hatalı), {Ms} ms",
            lane, shipments.Count, scans.Count, deferred, failed, stopwatch.ElapsedMilliseconds);

        return scans.Count;
    }
}
