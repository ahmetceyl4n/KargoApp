using Volo.Abp.Modularity;

namespace KargoApp;

[DependsOn(
    typeof(KargoAppDomainModule),
    typeof(KargoAppTestBaseModule)
)]
public class KargoAppDomainTestModule : AbpModule
{

}
