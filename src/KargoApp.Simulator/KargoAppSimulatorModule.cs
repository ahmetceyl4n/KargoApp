using KargoApp.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;

namespace KargoApp.Simulator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(KargoAppEntityFrameworkCoreModule)   // Domain + DB buradan gelir
)]
public class KargoAppSimulatorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<SimulatorOptions>(context.Services.GetConfiguration().GetSection("Simulator"));

        // Simülatör sadece veri hazırlar ve mesaj basar; ABP'nin arka plan işleri burada çalışmasın
        Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);
        Configure<AbpBackgroundWorkerOptions>(options => options.IsEnabled = false);
    }
}
