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
    private readonly DeadLetterPublisher _deadLetterPublisher;
    private readonly IClock _clock;
    private readonly ILogger<ScanEtoHandler> _logger;

    public ScanEtoHandler(IRepository<IncomingScan, Guid> incomingScanRepository,
                          IUnitOfWorkManager unitOfWorkManager,
                          DeadLetterPublisher deadLetterPublisher,
                          IClock clock,
                          ILogger<ScanEtoHandler> logger)
    {
        _incomingScanRepository = incomingScanRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _deadLetterPublisher = deadLetterPublisher;
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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && IsPermanentDataError(pg.SqlState))
        {
            // Veri bozuk: kaç kez denesen aynı sonuç. Dead-letter'a taşı, hata fırlatma → ABP ack gönderir.
            // (Hata fırlatsaydık ABP mesajı geri koyardı ve sonsuz döngü olurdu.)
            await _deadLetterPublisher.SendAsync(eventData, $"{pg.SqlState}: {pg.MessageText}");
            _logger.LogWarning("Zehirli mesaj dead-letter'a taşındı {ScanId}: {Error}", eventData.ScanId, pg.MessageText);
        }
        // Diğer her hata (DB koptu, zaman aşımı) yakalanmaz → ABP geri koyar → tekrar denenir
    }

    // PostgreSQL hata sınıfları: 22 = veri hatası (değer çok uzun, geçersiz format),
    // 23 = kısıt ihlali (unique yukarıda ayrıca yakalandı; catch sırası bu yüzden önemli)
    private static bool IsPermanentDataError(string sqlState)
    {
        return sqlState.StartsWith("22") || sqlState.StartsWith("23");
    }
}
