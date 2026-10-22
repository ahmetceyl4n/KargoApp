using KargoApp.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;

namespace KargoApp.Worker;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpEventBusRabbitMqModule),
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

        // Zehirli mesaj: ABP hata durumunda mesajı hep kuyruğa geri koyar (nack, requeue: true).
        // Quorum kuyruğu teslim sayısını sayar; limit aşılınca mesajı dead-letter exchange'ine gönderir.
        // (Sayı tipi önemli olduğu için appsettings yerine kodda; dead-letter exchange/kuyruğu ABP oluşturmaz, elle kuruldu.)
        Configure<AbpRabbitMqEventBusOptions>(options =>
        {
            options.QueueArguments["x-queue-type"] = "quorum";
            options.QueueArguments["x-delivery-limit"] = 5;
            options.QueueArguments["x-dead-letter-exchange"] = "KargoApp.DeadLetter";
        });

        // Worker açılınca şeritler başlasın
        context.Services.AddHostedService<ScanBatchProcessor>();
        context.Services.AddHostedService<OrphanScanSweeper>();
        context.Services.AddHostedService<StatsReporterService>();

        // ABP'nin hazır arka plan işleri (iş kuyruğu, token temizliği) API'nin işi; worker'da kapalı
        Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);
        Configure<AbpBackgroundWorkerOptions>(options => options.IsEnabled = false);
    }
}
