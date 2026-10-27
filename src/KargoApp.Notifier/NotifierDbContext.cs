using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;

namespace KargoApp.Notifier;

// Notifier'ın kendi veritabanı (KargoNotifier). İçinde sadece ABP'nin inbox tablosu var.
[ConnectionStringName("Notifier")]
public class NotifierDbContext : AbpDbContext<NotifierDbContext>, IHasEventInbox
{
    // ABP inbox: gelen mesaj önce buraya yazılır, sonra InboxProcessor handler'ı çağırır
    public DbSet<IncomingEventRecord> IncomingEvents { get; set; }

    public NotifierDbContext(DbContextOptions<NotifierDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureEventInbox();   // AbpEventInbox tablosu
    }
}
