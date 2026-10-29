using FileForge.Application.Conversions;
using FileForge.Application.Jobs;
using FileForge.Infrastructure;
using FileForge.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "Connection string 'Postgres' is not configured. Set it in the user-secrets of FileForge.Worker.");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddSingleton<ConversionRegistry>();
builder.Services.AddScoped<ConversionProcessor>();
builder.Services.AddHostedService<ConversionWorker>();

builder.Build().Run();
