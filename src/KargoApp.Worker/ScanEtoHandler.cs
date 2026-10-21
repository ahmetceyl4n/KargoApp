using System;
using System.Threading.Tasks;
using KargoApp.Shipments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace KargoApp.Worker;

// Okutmayı işlemez; sadece "bekleyen okutmalar" tablosuna yazar. İşleme ScanBatchProcessor'da.
public class ScanEtoHandler : IDistributedEventHandler<ScanEto>, ITransientDependency
{
    private readonly IRepository<IncomingScan, Guid> _incomingScanRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IClock _clock;
    private readonly ILogger<ScanEtoHandler> _logger;

    public ScanEtoHandler(IRepository<IncomingScan, Guid> incomingScanRepository,
                          IUnitOfWorkManager unitOfWorkManager,
                          IClock clock,
                          ILogger<ScanEtoHandler> logger)
    {
        _incomingScanRepository = incomingScanRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _clock = clock;
        _logger = logger;
    }

    public async Task HandleEventAsync(ScanEto eventData)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);

        await _incomingScanRepository.InsertAsync(new IncomingScan(
            eventData.ScanId, eventData.TrackingNumber, eventData.BranchCode,
            eventData.MovementType, eventData.OccurredAt, _clock.Now));

        try
        {
            await uow.CompleteAsync();   // DB'ye yazıldı → metot biter → ABP ack gönderir
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Aynı ScanId zaten tabloda: mesaj ikinci kez gelmiş. Sorun değil, geç (idempotency).
            _logger.LogDebug("Okutma zaten alınmış: {ScanId}", eventData.ScanId);
        }
    }
}
