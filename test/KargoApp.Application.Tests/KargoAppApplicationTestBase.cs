using Volo.Abp.Modularity;

namespace KargoApp;

public abstract class KargoAppApplicationTestBase<TStartupModule> : KargoAppTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
