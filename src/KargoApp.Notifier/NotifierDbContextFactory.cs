using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace KargoApp.Notifier;

// Sadece "dotnet ef migrations add" için: aracın uygulamayı başlatmadan DbContext kurabilmesi
public class NotifierDbContextFactory : IDesignTimeDbContextFactory<NotifierDbContext>
{
    public NotifierDbContext CreateDbContext(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.secrets.json", optional: true)
            .Build();

        var builder = new DbContextOptionsBuilder<NotifierDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Notifier"));

        return new NotifierDbContext(builder.Options);
    }
}
