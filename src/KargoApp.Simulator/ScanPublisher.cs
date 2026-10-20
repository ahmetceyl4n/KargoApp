using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KargoApp.Shipments;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace KargoApp.Simulator;

// Okutmaları paralel yayınlar: kargolar aynı anda en fazla N tane, bir kargonun okutmaları sırayla.
public class ScanPublisher : ITransientDependency
{
    private readonly IDistributedEventBus _eventBus;
    private int _sent;

    public ScanPublisher(IDistributedEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    // İlerleme raporu başka bir görevden okuyor: güncel değeri oku
    public int Sent => Volatile.Read(ref _sent);

    public async Task PublishAsync(IReadOnlyList<List<ScanEto>> scansByShipment,
                                   int parallelism, CancellationToken cancellationToken)
    {
        // 1) Gişeler: aynı anda en fazla 'parallelism' kargo gönderilir
        using var semaphore = new SemaphoreSlim(parallelism);

        // 2) Her kargo için bir görev
        var tasks = scansByShipment.Select(async scans =>
        {
            // 3) Sıra bekle (thread'i bloklamaz; Ctrl+C beklemeyi keser)
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // 4) Bir kargonun okutmaları sırayla gider
                foreach (var scan in scans)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _eventBus.PublishAsync(scan);   // ABP: JSON'a çevir, KargoApp exchange'ine KargoApp.Scan anahtarıyla gönder
                    Interlocked.Increment(ref _sent);     // birden çok görev aynı anda artırıyor: atomik olmalı
                }
            }
            finally
            {
                semaphore.Release();                      // hata olsa da gişeyi bırak
            }
        });

        // 5) Hepsinin bitmesini bekle
        await Task.WhenAll(tasks);
    }
}
