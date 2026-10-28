using FileForge.Api.Middleware;
using FileForge.Application.Conversions;
using FileForge.Application.Errors;
using FileForge.Application.Jobs;
using FileForge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Not in appsettings.json: locally it comes from user-secrets, in Docker from ConnectionStrings__Postgres.
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "Connection string 'Postgres' is not configured. " +
        "Run: dotnet user-secrets set \"ConnectionStrings:Postgres\" \"<connection string>\" --project src/FileForge.Api");

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    // Errors produced by the framework itself (validation, body too large, crashes) get a code too,
    // so every error response has the same shape. Our own codes are set earlier and are kept.
    var code = context.ProblemDetails.Status switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
        StatusCodes.Status413PayloadTooLarge => ErrorCodes.FileTooLarge,
        >= 500 => ErrorCodes.InternalError,
        _ => null,
    };

    if (code is not null)
    {
        context.ProblemDetails.Extensions.TryAdd("code", code);
    }
});
builder.Services.AddExceptionHandler<AppExceptionHandler>();

builder.Services.AddOptions<ConversionOptions>()
    .BindConfiguration(ConversionOptions.SectionName)
    .Validate(o => o.MaxFileSizeBytes > 0 && o.Retention > TimeSpan.Zero, "Invalid 'Conversions' settings.")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddSingleton<ConversionRegistry>();
builder.Services.AddScoped<CreateConversionService>();
builder.Services.AddScoped<GetConversionService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "FileForge API v1"));
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
