using Azure.Messaging.ServiceBus;
using LedgeLink.Distributor.API.API.Middleware;
using LedgeLink.Distributor.API.Application.Interfaces;
using LedgeLink.Distributor.API.Application.UseCases;
using LedgeLink.Distributor.API.Infrastructure.Messaging;
using LedgeLink.Distributor.API.Infrastructure.Persistence;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// ── Aspire Service Defaults ──────────────────────────────────────────────────
builder.AddServiceDefaults();

// ── MongoDB ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IMongoClient>(
    new MongoClient(builder.Configuration.GetConnectionString("ledgelink")));


// ── Service Bus - Let Aspire inject the connection ──────────────────────────
builder.Services.AddSingleton(
    new ServiceBusClient(builder.Configuration.GetConnectionString("messaging")));

// ── Dependency Injection ─────────────────────────────────────────────────────
builder.Services.AddScoped<ITradeRepository, MongoTradeRepository>();
builder.Services.AddScoped<ITradePublisher, ServiceBusTradePublisher>();
builder.Services.AddScoped<SubmitTradeUseCase>();

// ── Swagger ──────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "LedgeLink — Distributor API",
        Version     = "v1",
        Description = "Trade submission endpoint for Hargreaves Lansdown."
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// ── Middleware ───────────────────────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "LedgeLink Distributor API v1");
    c.RoutePrefix = string.Empty;
});
app.UseRouting();
app.MapControllers();

// ── Startup ──────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var repo = (MongoTradeRepository)scope.ServiceProvider.GetRequiredService<ITradeRepository>();
    await repo.EnsureIndexesAsync();
}

app.Run();