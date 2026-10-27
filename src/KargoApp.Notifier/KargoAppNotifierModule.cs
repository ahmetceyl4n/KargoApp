using System;
using System.Threading.Tasks;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;

namespace KargoApp.Notifier;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(AbpDistributedLockingModule),
    typeof(KargoAppDomainSharedModule)   // sadece Eto'lar
)]
public class KargoAppNotifierModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);   // ana projeyle aynı tarih davranışı
        var configuration = context.Services.GetConfiguration();

        context.Services.AddAbpDbContext<NotifierDbContext>();
        Configure<AbpDbContextOptions>(options => options.UseNpgsql());

        // Inbox: RabbitMQ'dan gelen mesaj handler'a değil önce AbpEventInbox'a yazılır, sonra ack.
        // Handler'ı InboxProcessor (ABP background worker) sonra çağırır.
        Configure<AbpDistributedEventBusOptions>(options =>
        {
            options.Inboxes.Configure(config =>
            {
                config.UseDbContext<NotifierDbContext>();
            });
        });

        // Handler hata verirse: olayı atla, sonra tekrar dene (2, 4, 8, 16 sn…); 5 denemeden sonra bırak.
        // Varsayılan (Retry) aynı olayı tekrar tekrar dener ve arkasındakileri bekletir; bildirimde sıra önemli değil.
        Configure<AbpEventBusBoxesOptions>(options =>
        {
            options.InboxProcessorFailurePolicy = InboxProcessorFailurePolicy.RetryLater;
            options.InboxProcessorRetryBackoffFactor = 2;
            options.InboxProcessorMaxRetryCount = 5;
        });

        // InboxProcessor her turda Redis kilidi alır: iki Notifier aynı satırı iki kez işlemesin
        context.Services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]!));
        context.Services.AddSingleton<IDistributedLockProvider>(sp =>
            new RedisDistributedSynchronizationProvider(sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase()));
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        // Açılışta tabloyu oluştur/güncelle (ayrı DB, DbMigrator'ın işi değil). Veritabanı yoksa Npgsql oluşturur.
        using var scope = context.ServiceProvider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<NotifierDbContext>().Database.MigrateAsync();
    }
}
