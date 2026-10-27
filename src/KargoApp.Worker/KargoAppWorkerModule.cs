using KargoApp.EntityFrameworkCore;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict.Tokens;

namespace KargoApp.Worker;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(AbpDistributedLockingModule),
    typeof(KargoAppEntityFrameworkCoreModule)   // DB + Domain (Domain.Shared da bununla gelir)
)]
public class KargoAppWorkerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        Configure<ScanProcessingOptions>(configuration.GetSection("ScanProcessing"));

        // Redis bağlantısı: tek nesne, bütün program paylaşır (StackExchange.Redis böyle önerir).
        // ABP'nin cache modülü HINCRBY gibi Redis'e özgü komutları sunmadığı için istemciyi doğrudan kullanıyoruz.
        context.Services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]!));

        // Distributed lock Redis'te: OutboxSender her turda "outbox'ı ben göndereyim" kilidi alır.
        // Yerel kilit kalsaydı iki worker aynı outbox satırlarını birlikte gönderirdi (çift bildirim).
        context.Services.AddSingleton<IDistributedLockProvider>(sp =>
            new RedisDistributedSynchronizationProvider(sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase()));

        // Zehirli mesaj: ABP hata durumunda mesajı hep kuyruğa geri koyar (nack, requeue: true).
        // Quorum kuyruğu teslim sayısını sayar; limit aşılınca mesajı dead-letter exchange'ine gönderir.
        // (Sayı tipi önemli olduğu için appsettings yerine kodda; dead-letter exchange/kuyruğu ABP oluşturmaz, elle kuruldu.)
        Configure<AbpRabbitMqEventBusOptions>(options =>
        {
            options.QueueArguments["x-queue-type"] = "quorum";
            options.QueueArguments["x-delivery-limit"] = 5;
            options.QueueArguments["x-dead-letter-exchange"] = "KargoApp.DeadLetter";
        });

        // Outbox: UoW içindeki PublishAsync artık RabbitMQ'ya değil, AbpEventOutbox tablosuna yazar (aynı transaction).
        // Inbox yok: worker'ın aldığı okutmalar zaten kendi rafımıza (AppIncomingScans) düşüyor.
        Configure<AbpDistributedEventBusOptions>(options =>
        {
            options.Outboxes.Configure(config =>
            {
                config.UseDbContext<KargoAppDbContext>();
            });
        });

        // Worker açılınca şeritler başlasın
        context.Services.AddHostedService<ScanBatchProcessor>();
        context.Services.AddHostedService<OrphanScanSweeper>();
        context.Services.AddHostedService<StatsReporterService>();

        // ABP'nin iş kuyruğu API'nin işi; worker'da kapalı
        Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);

        // Background worker'lar açık: outbox'ı RabbitMQ'ya taşıyan OutboxSenderManager da bir ABP background worker.
        // Kapalı kalırsa outbox tablosu dolar ama hiçbir şey gönderilmez.
        Configure<AbpBackgroundWorkerOptions>(options => options.IsEnabled = true);
        // Açılınca OpenIddict'in token temizliği de çalışırdı; o API'nin işi
        Configure<TokenCleanupOptions>(options => options.IsCleanupEnabled = false);
    }
}
