using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;
using Volo.Abp.DependencyInjection;

namespace KargoApp.Worker;

// Bütün worker'ların ortak sayacı. Redis'te tutulur; HINCRBY tek komut ve atomik olduğu için
// iki ayrı program aynı anda artırsa da hiçbir sayım kaybolmaz.
public class SharedScanCounter : ISingletonDependency
{
    private const string ProcessedKey = "kargoapp:stats:processed";   // hash: şube → işlenen
    private const string TotalsKey = "kargoapp:stats:totals";         // hash: failed, deferred

    private readonly IDatabase _redis;

    public SharedScanCounter(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    public async Task RecordAsync(IEnumerable<string> processedBranches, int failed, int deferred)
    {
        // Şube başına tek komut: 200 hareket için 200 değil, ~6 HINCRBY
        var tasks = processedBranches
            .GroupBy(branch => branch)
            .Select(g => (Task)_redis.HashIncrementAsync(ProcessedKey, g.Key, g.Count()))
            .ToList();

        if (failed > 0)
        {
            tasks.Add(_redis.HashIncrementAsync(TotalsKey, "failed", failed));
        }

        if (deferred > 0)
        {
            tasks.Add(_redis.HashIncrementAsync(TotalsKey, "deferred", deferred));
        }

        await Task.WhenAll(tasks);   // komutlar aynı anda gönderilir, hepsi beklenir
    }

    public async Task<IReadOnlyDictionary<string, long>> GetProcessedByBranchAsync()
    {
        var entries = await _redis.HashGetAllAsync(ProcessedKey);
        return entries.ToDictionary(e => e.Name.ToString(), e => (long)e.Value);
    }

    public async Task<(long Failed, long Deferred)> GetTotalsAsync()
    {
        var failed = await _redis.HashGetAsync(TotalsKey, "failed");
        var deferred = await _redis.HashGetAsync(TotalsKey, "deferred");
        return ((long?)failed ?? 0, (long?)deferred ?? 0);
    }
}
