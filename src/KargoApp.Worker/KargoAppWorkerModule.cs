using Volo.Abp.Autofac;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;

namespace KargoApp.Worker;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(KargoAppDomainSharedModule)
)]
public class KargoAppWorkerModule : AbpModule
{
}
