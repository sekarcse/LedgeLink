using Azure.Messaging.ServiceBus;
using LedgeLink.Validator.Worker;
using LedgeLink.Validator.Worker.Application.Interfaces;
using LedgeLink.Validator.Worker.Application.Services;
using LedgeLink.Validator.Worker.Infrastructure.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// ── Service Bus Configuration ─────────────────────────────────────────────────
var serviceBusConnection = builder.Configuration["ServiceBus:ConnectionString"]
    ?? "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDeveloperTokenProvider=true";
builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection));

// DI wiring:
//   ValidatorWorker (hosted)
//       └── TradeValidationService  (Application)
//               └── IMessagePublisher ← ServiceBusMessagePublisher (Infrastructure)
builder.Services.AddSingleton<ServiceBusMessagePublisher>();
builder.Services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<ServiceBusMessagePublisher>());
builder.Services.AddSingleton<TradeValidationService>();
builder.Services.AddHostedService<ValidatorWorker>();

var host = builder.Build();
host.Run();
