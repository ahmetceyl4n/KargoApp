using KargoApp.Samples;
using Xunit;

namespace KargoApp.EntityFrameworkCore.Applications;

[Collection(KargoAppTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<KargoAppEntityFrameworkCoreTestModule>
{

}
