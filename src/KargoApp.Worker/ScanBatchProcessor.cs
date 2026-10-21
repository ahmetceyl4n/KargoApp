using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KargoApp.Worker;

// Şeritleri döndüren müdür: N şerit aynı anda, her biri sürekli paket işler.
public class ScanBatchProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScanProcessingOptions _options;
    private readonly ILogger<ScanBatchProcessor> _logger;

    public ScanBatchProcessor(IServiceScopeFactory scopeFactory,
                              IOptions<ScanProcessingOptions> options,
                              ILogger<ScanBatchProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Lanes} şerit başlıyor (paket: en fazla {Batch} kargo)",
            _options.Lanes, _options.ShipmentsPerBatch);

        // N şerit aynı anda; hepsi bitince (Ctrl+C) servis durur
        var lanes = Enumerable.Range(1, _options.Lanes)
            .Select(lane => RunLaneAsync(lane, stoppingToken));

        return Task.WhenAll(lanes);
    }

    // Tek bir şeridin hayatı: Ctrl+C gelene kadar paket işle
    private async Task RunLaneAsync(int lane, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                // Her paket kendi scope'unda: kendi DbContext'i, kendi transaction'ı
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<ScanBatchHandler>();
                processed = await handler.ProcessNextBatchAsync(lane, _options.ShipmentsPerBatch);
            }
            catch (Exception ex)
            {
                // DB hatası vb.: transaction geri alındı, okutmalar bekliyor; sonraki turda tekrar denenir
                _logger.LogError(ex, "Şerit {Lane}: paket işlenemedi", lane);
            }

            if (processed == 0)
            {
                // İş yok (ya da hata oldu): biraz bekle, DB'yi boş sorgularla yorma
                try
                {
                    await Task.Delay(_options.IdleDelayMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Ctrl+C geldi: beklemeyi bırak, while koşulu döngüden çıkarır
                }
            }
        }

        _logger.LogInformation("Şerit {Lane} durdu", lane);
    }
}
