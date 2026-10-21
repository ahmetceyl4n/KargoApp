using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KargoApp.Worker;

// Her 10 saniyede bir ortak defterden özet log basar. DB'ye gitmez.
public class StatsReporterService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly ScanStats _stats;
    private readonly ILogger<StatsReporterService> _logger;

    public StatsReporterService(ScanStats stats, ILogger<StatsReporterService> logger)
    {
        _stats = stats;
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
                _logger.LogInformation("Özet: toplam {Total} (son 10 sn: +{Delta}), hatalı {Failed} | {Branches}",
                    total, total - lastTotal, _stats.Failed, branches);

                lastTotal = total;
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C
        }
    }
}
