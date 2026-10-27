using FileForge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// The connection string is NOT in appsettings.json: locally it comes from user-secrets,
// in Docker it comes from an environment variable (ConnectionStrings__Postgres).
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "Connection string 'Postgres' is not configured. " +
        "Run: dotnet user-secrets set \"ConnectionStrings:Postgres\" \"<connection string>\" --project src/FileForge.Api");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
