using Azure.Messaging.ServiceBus;
using LedgeLink.Validator.Worker;
using LedgeLink.Validator.Worker.Application.Interfaces;
using LedgeLink.Validator.Worker.Application.Services;
using LedgeLink.Validator.Worker.Infrastructure.Messaging;

var builder = Host.CreateApplicationBuilder(args);

// ── Aspire Service Defaults ──────────────────────────────────────────────────
builder.AddServiceDefaults();

// ── Service Bus - Let Aspire inject the connection ──────────────────────────
var serviceBusConnectionString = builder.Configuration.GetConnectionString("messaging")
    ?? builder.Configuration["messaging"];

builder.Services.AddSingleton(new ServiceBusClient(
    serviceBusConnectionString,
    new ServiceBusClientOptions
    {
        TransportType = ServiceBusTransportType.AmqpTcp
    }));

// ── Dependency Injection ─────────────────────────────────────────────────────
builder.Services.AddSingleton<IMessagePublisher, ServiceBusMessagePublisher>();
builder.Services.AddSingleton<TradeValidationService>();
builder.Services.AddHostedService<ValidatorWorker>();

var host = builder.Build();
host.Run();