using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KargoApp.Branches;
using KargoApp.Simulator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Volo.Abp", LogEventLevel.Warning)
    .WriteTo.Async(c => c.Console())
    .CreateLogger();

// Ctrl+C programı hemen öldürmesin; token'ı iptal etsin, kod düzgünce dursun
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Configuration.AddJsonFile("appsettings.secrets.json", optional: true);
    // Örnek: dotnet run -- --shipments 1000 --movements 4 --parallelism 10
    builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
    {
        ["--shipments"] = "Simulator:Shipments",
        ["--movements"] = "Simulator:MovementsPerShipment",
        ["--parallelism"] = "Simulator:Parallelism"
    });
    builder.Logging.ClearProviders().AddSerilog();
    builder.ConfigureContainer(builder.Services.AddAutofacServiceProviderFactory());

    await builder.Services.AddApplicationAsync<KargoAppSimulatorModule>(options =>
    {
        options.Services.ReplaceConfiguration(builder.Configuration);
    });

    using var host = builder.Build();
    await host.InitializeAsync();   // ABP modülleri başlar: DB ve RabbitMQ bağlantıları hazır

    var services = host.Services;
    var options = services.GetRequiredService<IOptions<SimulatorOptions>>().Value;
    var publisher = services.GetRequiredService<ScanPublisher>();

    try
    {
        Log.Information("1/2 {Count} kargo oluşturuluyor...", options.Shipments);
        var trackingNumbers = await services.GetRequiredService<ShipmentSeeder>()
            .CreateAsync(options.Shipments, cts.Token);

        var branchCodes = (await services.GetRequiredService<IRepository<Branch, string>>().GetListAsync())
            .Select(b => b.Id)
            .ToList();

        var scansByShipment = trackingNumbers
            .Select(t => ScanFactory.Create(t, options.MovementsPerShipment, branchCodes, DateTime.UtcNow))
            .ToList();
        var total = scansByShipment.Sum(s => s.Count);

        Log.Information("2/2 {Total} okutma yayınlanıyor (paralellik {Parallelism})...",
            total, options.Parallelism);

        var stopwatch = Stopwatch.StartNew();
        using var stopReporting = new CancellationTokenSource();
        var reporting = ReportProgressAsync(publisher, total, stopwatch, stopReporting.Token);

        try
        {
            await publisher.PublishAsync(scansByShipment, options.Parallelism, cts.Token);
        }
        finally
        {
            stopReporting.Cancel();
            await reporting;
        }

        Log.Information("Bitti: {Sent} okutma {Seconds:F1} sn içinde, saniyede {Rate:F0} mesaj",
            publisher.Sent, stopwatch.Elapsed.TotalSeconds, publisher.Sent / stopwatch.Elapsed.TotalSeconds);
    }
    catch (OperationCanceledException)
    {
        Log.Warning("Durduruldu (Ctrl+C). Gönderilen okutma: {Sent}", publisher.Sent);
    }

    await services.GetRequiredService<IAbpApplicationWithExternalServiceProvider>().ShutdownAsync();
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Her saniye ilerlemeyi yazar; gönderimi yapan görevlerden bağımsız çalışır
static async Task ReportProgressAsync(ScanPublisher publisher, int total, Stopwatch stopwatch,
                                      CancellationToken cancellationToken)
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
    try
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var sent = publisher.Sent;
            Log.Information("{Sent}/{Total} gönderildi, saniyede {Rate:F0} mesaj",
                sent, total, sent / stopwatch.Elapsed.TotalSeconds);
        }
    }
    catch (OperationCanceledException)
    {
        // Gönderim bitti, rapor da durur
    }
}
