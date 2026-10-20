using System.Threading.Tasks;
using KargoApp.Shipments;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace KargoApp.Worker;

public class ScanEtoHandler : IDistributedEventHandler<ScanEto>, ITransientDependency
{
    private readonly ILogger<ScanEtoHandler> _logger;

    public ScanEtoHandler(ILogger<ScanEtoHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleEventAsync(ScanEto eventData)
    {
        _logger.LogInformation("Okutma alındı: {TrackingNumber} {MovementType} @ {BranchCode}",
            eventData.TrackingNumber, eventData.MovementType, eventData.BranchCode);
        return Task.CompletedTask;
    }
}
