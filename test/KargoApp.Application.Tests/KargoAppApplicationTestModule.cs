using Volo.Abp.Modularity;

namespace KargoApp;

[DependsOn(
    typeof(KargoAppApplicationModule),
    typeof(KargoAppDomainTestModule)
)]
public class KargoAppApplicationTestModule : AbpModule
{

}
