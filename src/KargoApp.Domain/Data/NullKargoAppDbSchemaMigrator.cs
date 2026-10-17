using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace KargoApp.Data;

/* This is used if database provider does't define
 * IKargoAppDbSchemaMigrator implementation.
 */
public class NullKargoAppDbSchemaMigrator : IKargoAppDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
