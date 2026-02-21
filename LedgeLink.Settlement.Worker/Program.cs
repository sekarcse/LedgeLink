using LedgeLink.Settlement.Worker;
using LedgeLink.Settlement.Worker.Application.Interfaces;
using LedgeLink.Settlement.Worker.Application.Services;
using LedgeLink.Settlement.Worker.Infrastructure.Messaging;
using LedgeLink.Settlement.Worker.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

// ── Aspire Service Defaults ──────────────────────────────────────────────────
builder.AddServiceDefaults();

// ── MongoDB ──────────────────────────────────────────────────────────────────
builder.AddMongoDBClient("ledgelink");

// ── Service Bus - Let Aspire inject the connection ──────────────────────────
builder.AddAzureServiceBusClient("messaging");

// ── Dependency Injection ─────────────────────────────────────────────────────
builder.Services.AddSingleton<ITradeSettlementRepository, MongoTradeSettlementRepository>();
builder.Services.AddSingleton<ISettlementPublisher, ServiceBusSettlementPublisher>();
builder.Services.AddSingleton<SettleTradeService>();
builder.Services.AddHostedService<SettlementWorker>();

var host = builder.Build();
host.Run();