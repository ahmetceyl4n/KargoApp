using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using KargoApp.Data;
using Volo.Abp.DependencyInjection;

namespace KargoApp.EntityFrameworkCore;

public class EntityFrameworkCoreKargoAppDbSchemaMigrator
    : IKargoAppDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreKargoAppDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolving the KargoAppDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<KargoAppDbContext>()
            .Database
            .MigrateAsync();
    }
}
