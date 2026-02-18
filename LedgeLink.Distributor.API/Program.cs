using Azure.Messaging.ServiceBus;
using LedgeLink.Distributor.API.API.Middleware;
using LedgeLink.Distributor.API.Application.Interfaces;
using LedgeLink.Distributor.API.Application.UseCases;
using LedgeLink.Distributor.API.Infrastructure.Messaging;
using LedgeLink.Distributor.API.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ── Aspire ───────────────────────────────────────────────────────────────────
builder.AddServiceDefaults();
builder.AddMongoDBClient("ledgelink");

// ── Service Bus Configuration ─────────────────────────────────────────────────
var serviceBusConnection = builder.Configuration["ServiceBus:ConnectionString"]
    ?? "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDeveloperTokenProvider=true";
builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection));

// ── Dependency Injection — Dependency Inversion in action ───────────────────
//
//   Application layer interfaces  →  Infrastructure layer implementations
//
//   TradesController
//       └── SubmitTradeUseCase          (Application)
//               ├── ITradeRepository   ← MongoTradeRepository      (Infrastructure)
//               └── ITradePublisher    ← ServiceBusTradePublisher  (Infrastructure)
//
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
        Description = "Trade submission endpoint for Hargreaves Lansdown. Implements idempotency, MongoDB persistence, and Azure Service Bus event publishing."
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// ── Middleware pipeline ──────────────────────────────────────────────────────
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

// ── Startup: ensure MongoDB indexes ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var repo = (MongoTradeRepository)scope.ServiceProvider.GetRequiredService<ITradeRepository>();
    await repo.EnsureIndexesAsync();
}

app.Run();
