using Volo.Abp.Modularity;

namespace KargoApp;

/* Inherit from this class for your domain layer tests. */
public abstract class KargoAppDomainTestBase<TStartupModule> : KargoAppTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
