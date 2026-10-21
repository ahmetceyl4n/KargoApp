using System;
using System.Threading;
using System.Threading.Tasks;
using KargoApp.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace KargoApp.Worker;

// Kargosu DB'de olmayan okutmaları bulur ve hatalı işaretler.
// Şeritler "kargoları" kilitlediği için bu okutmaları hiç görmez; temizlenmezse sonsuza kadar bekler.
public class OrphanScanSweeper : BackgroundService
{
    // Kaç saniyede bir bakılır
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    // Okutmaya tanınan süre: kargo kaydı okutmadan biraz sonra gelebilir
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrphanScanSweeper> _logger;

    public OrphanScanSweeper(IServiceScopeFactory scopeFactory, ILogger<OrphanScanSweeper> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await SweepAsync();
                }
                catch (Exception ex)
                {
                    // Sweeper ölmesin; 30 sn sonra tekrar dener
                    _logger.LogError(ex, "Sahipsiz okutma temizliği başarısız");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C
        }
    }

    private async Task SweepAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var dbContextProvider = scope.ServiceProvider.GetRequiredService<IDbContextProvider<KargoAppDbContext>>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var db = await dbContextProvider.GetDbContextAsync();

        var now = clock.Now;
        var cutoff = now - GracePeriod;

        // Tek bir UPDATE: kargosu olmayan, bekleme süresini aşmış okutmalar → hatalı
        var count = await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""AppIncomingScans"" i
            SET ""ProcessedAt"" = {now}, ""Error"" = {KargoAppDomainErrorCodes.ShipmentNotFound}
            WHERE i.""ProcessedAt"" IS NULL
              AND i.""ReceivedAt"" < {cutoff}
              AND NOT EXISTS (SELECT 1 FROM ""AppShipments"" s
                              WHERE s.""TrackingNumber"" = i.""TrackingNumber"" AND s.""IsDeleted"" = false)");

        await uow.CompleteAsync();

        if (count > 0)
        {
            _logger.LogWarning("{Count} okutmanın kargosu yok, hatalı işaretlendi", count);
        }
    }
}
