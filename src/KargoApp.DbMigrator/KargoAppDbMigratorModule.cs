using KargoApp.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace KargoApp.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(KargoAppEntityFrameworkCoreModule),
    typeof(KargoAppApplicationContractsModule)
)]
public class KargoAppDbMigratorModule : AbpModule
{
}
