using Ditak.Attendance.Core;
using Ditak.Attendance.Core.Data;
using Ditak.Attendance.Core.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");
var companyTimezone = builder.Configuration["Company:Timezone"] ?? "UTC";

builder.Services.AddAttendanceCore(connectionString, companyTimezone);
builder.Services.AddHostedService<RecalculationWorker>();

var host = builder.Build();
await host.RunAsync();

public sealed class RecalculationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecalculationWorker> _logger;

    public RecalculationWorker(IServiceScopeFactory scopeFactory, ILogger<RecalculationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for API migrations/seed on first boot.
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var attendance = scope.ServiceProvider.GetRequiredService<AttendanceService>();
                await attendance.RecalculateAllAsync(stoppingToken);
                _logger.LogInformation("Attendance recalculation completed at {Utc}", DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Attendance recalculation failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
