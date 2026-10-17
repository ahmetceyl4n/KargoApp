using KargoApp.Samples;
using Xunit;

namespace KargoApp.EntityFrameworkCore.Domains;

[Collection(KargoAppTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<KargoAppEntityFrameworkCoreTestModule>
{

}
