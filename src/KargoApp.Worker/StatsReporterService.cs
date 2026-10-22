using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KargoApp.Worker;

// Her 10 saniyede bir özet log basar: bu worker'ın kendi defteri + Redis'teki ortak defter. DB'ye gitmez.
public class StatsReporterService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly ScanStats _stats;
    private readonly SharedScanCounter _sharedCounter;
    private readonly ILogger<StatsReporterService> _logger;

    public StatsReporterService(ScanStats stats, SharedScanCounter sharedCounter, ILogger<StatsReporterService> logger)
    {
        _stats = stats;
        _sharedCounter = sharedCounter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        var lastTotal = 0;

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var byBranch = _stats.GetProcessedByBranch();
                var total = byBranch.Values.Sum();

                if (total == lastTotal)
                {
                    continue;   // yeni bir şey yoksa log'u kirletme
                }

                var branches = string.Join(", ", byBranch.OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Value}"));
                _logger.LogInformation("Özet (bu worker): toplam {Total} (son 10 sn: +{Delta}), erteleme {Deferred}, hatalı {Failed} | {Branches}",
                    total, total - lastTotal, _stats.Deferred, _stats.Failed, branches);

                lastTotal = total;

                // Redis'teki ortak defter: bütün worker'ların toplamı
                try
                {
                    var shared = await _sharedCounter.GetProcessedByBranchAsync();
                    var (sharedFailed, sharedDeferred) = await _sharedCounter.GetTotalsAsync();
                    var sharedBranches = string.Join(", ", shared.OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Value}"));
                    _logger.LogInformation("Özet (TÜM worker'lar, Redis): toplam {Total}, erteleme {Deferred}, hatalı {Failed} | {Branches}",
                        shared.Values.Sum(), sharedDeferred, sharedFailed, sharedBranches);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Raporcu ölmesin; Redis'e ulaşılamazsa sadece yerel özet basılır
                    _logger.LogWarning(ex, "Ortak sayaç okunamadı (Redis)");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C
        }
    }
}
