using LedgeLink.Validator.Worker;
using LedgeLink.Validator.Worker.Application.Interfaces;
using LedgeLink.Validator.Worker.Application.Services;
using LedgeLink.Validator.Worker.Infrastructure.Messaging;

var builder = Host.CreateApplicationBuilder(args);

// ── Aspire Service Defaults ──────────────────────────────────────────────────
builder.AddServiceDefaults();

// ── Service Bus - Let Aspire inject the connection ──────────────────────────
builder.AddAzureServiceBusClient("messaging");

// ── Dependency Injection ─────────────────────────────────────────────────────
builder.Services.AddSingleton<IMessagePublisher, ServiceBusMessagePublisher>();
builder.Services.AddSingleton<TradeValidationService>();
builder.Services.AddHostedService<ValidatorWorker>();

var host = builder.Build();
host.Run();