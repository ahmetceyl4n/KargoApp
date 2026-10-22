using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Volo.Abp.DependencyInjection;

namespace KargoApp.Worker;

// Bütün şeritlerin ortak sayaçları. Tek nesne (singleton): 4 şerit aynı deftere yazar.
// Aynı anda yazıldığı için içi thread-safe olmak zorunda.
public class ScanStats : ISingletonDependency
{
    private readonly ConcurrentDictionary<string, int> _processedByBranch = new();
    private int _failed;
    private int _deferred;

    public int Failed => Volatile.Read(ref _failed);

    // Erteleme sayısı (aynı okutma birden çok kez ertelenebilir)
    public int Deferred => Volatile.Read(ref _deferred);

    // Bir okutma işlendi: o şubenin sayacını 1 artır (yoksa 1 ile başlat)
    public void RecordProcessed(string branchCode)
    {
        _processedByBranch.AddOrUpdate(branchCode, 1, (_, count) => count + 1);
    }

    public void RecordFailed(int count)
    {
        Interlocked.Add(ref _failed, count);
    }

    public void RecordDeferred(int count)
    {
        Interlocked.Add(ref _deferred, count);
    }

    // Raporcu için o anki durumun kopyası (şeritler yazmaya devam ederken rahatça okunsun)
    public IReadOnlyDictionary<string, int> GetProcessedByBranch()
    {
        return new Dictionary<string, int>(_processedByBranch);
    }
}
