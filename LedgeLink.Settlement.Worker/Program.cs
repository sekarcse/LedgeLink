using Azure.Messaging.ServiceBus;
using LedgeLink.Settlement.Worker;
using LedgeLink.Settlement.Worker.Application.Interfaces;
using LedgeLink.Settlement.Worker.Application.Services;
using LedgeLink.Settlement.Worker.Infrastructure.Messaging;
using LedgeLink.Settlement.Worker.Infrastructure.Persistence;
using MongoDB.Driver;

var builder = Host.CreateApplicationBuilder(args);

// ── Aspire Service Defaults ──────────────────────────────────────────────────
builder.AddServiceDefaults();

// ── MongoDB ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IMongoClient>(
    new MongoClient(builder.Configuration.GetConnectionString("ledgelink")));

// ── Service Bus - Let Aspire inject the connection ──────────────────────────
var serviceBusConnectionString = builder.Configuration.GetConnectionString("messaging")
    ?? builder.Configuration["messaging"];

builder.Services.AddSingleton(new ServiceBusClient(
    serviceBusConnectionString,
    new ServiceBusClientOptions
    {
        TransportType = ServiceBusTransportType.AmqpTcp  // ← correct for emulator
    }));

builder.Services.AddSingleton(new ServiceBusClient(
    serviceBusConnectionString, 
    new ServiceBusClientOptions
    {
        TransportType = ServiceBusTransportType.AmqpWebSockets  // required for emulator
    }));

// ── Dependency Injection ─────────────────────────────────────────────────────
builder.Services.AddSingleton<ITradeSettlementRepository, MongoTradeSettlementRepository>();
builder.Services.AddSingleton<ISettlementPublisher, ServiceBusSettlementPublisher>();
builder.Services.AddSingleton<SettleTradeService>();
builder.Services.AddHostedService<SettlementWorker>();

var host = builder.Build();
host.Run();