using KargoApp.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Volo.Abp;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Volo.Abp", LogEventLevel.Warning)
    .WriteTo.Async(c => c.Console())
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Configuration.AddJsonFile("appsettings.secrets.json", optional: true);
    builder.Logging.ClearProviders().AddSerilog();
    builder.ConfigureContainer(builder.Services.AddAutofacServiceProviderFactory());

    await builder.Services.AddApplicationAsync<KargoAppWorkerModule>(options =>
    {
        options.Services.ReplaceConfiguration(builder.Configuration);
    });

    var host = builder.Build();
    await host.InitializeAsync();   // ABP modülleri burada başlar: RabbitMQ'ya bağlanılır, kuyruk dinlenmeye başlar
    await host.RunAsync();          // Ctrl+C gelene kadar bekler
}
finally
{
    await Log.CloseAndFlushAsync();
}
