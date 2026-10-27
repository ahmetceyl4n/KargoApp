using System;
using System.Threading.Tasks;
using KargoApp.Shipments;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace KargoApp.Notifier;

// Teslim edilen kargo için müşteriye bildirim (gerçekte SMS/e-posta; biz log yazıyoruz).
// RabbitMQ'dan doğrudan çağrılmaz: ABP mesajı önce AbpEventInbox'a yazar, InboxProcessor sonra burayı çağırır.
public class ShipmentDeliveredHandler : IDistributedEventHandler<ShipmentDeliveredEto>, ITransientDependency
{
    // Deney için: SMS sağlayıcısının ara sıra cevap vermediğini taklit eder (%20)
    private const int FailurePercent = 20;

    private readonly ILogger<ShipmentDeliveredHandler> _logger;

    public ShipmentDeliveredHandler(ILogger<ShipmentDeliveredHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleEventAsync(ShipmentDeliveredEto eventData)
    {
        if (Random.Shared.Next(100) < FailurePercent)
        {
            _logger.LogWarning("📵 SMS gönderilemedi {TrackingNumber}, ABP tekrar deneyecek", eventData.TrackingNumber);
            throw new InvalidOperationException("SMS sağlayıcısı cevap vermedi (deney)");
        }

        _logger.LogInformation("📩 {TrackingNumber} teslim edildi ({BranchCode}, {DeliveredAt:HH:mm:ss}): müşteriye SMS gönderildi",
            eventData.TrackingNumber, eventData.BranchCode, eventData.DeliveredAt);

        return Task.CompletedTask;
    }
}
