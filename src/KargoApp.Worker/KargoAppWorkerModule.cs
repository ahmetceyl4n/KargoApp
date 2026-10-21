using KargoApp.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        Configure<ScanProcessingOptions>(context.Services.GetConfiguration().GetSection("ScanProcessing"));

        // Worker açılınca şeritler başlasın
        context.Services.AddHostedService<ScanBatchProcessor>();
        context.Services.AddHostedService<OrphanScanSweeper>();
        context.Services.AddHostedService<StatsReporterService>();

        // ABP'nin hazır arka plan işleri (iş kuyruğu, token temizliği) API'nin işi; worker'da kapalı
        Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);
        Configure<AbpBackgroundWorkerOptions>(options => options.IsEnabled = false);
    }
}
