using Xunit;

namespace KargoApp.EntityFrameworkCore;

[CollectionDefinition(KargoAppTestConsts.CollectionDefinitionName)]
public class KargoAppEntityFrameworkCoreCollection : ICollectionFixture<KargoAppEntityFrameworkCoreFixture>
{

}
