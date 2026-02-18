using Azure.Messaging.ServiceBus;
using LedgeLink.Settlement.Worker;
using LedgeLink.Settlement.Worker.Application.Interfaces;
using LedgeLink.Settlement.Worker.Application.Services;
using LedgeLink.Settlement.Worker.Infrastructure.Messaging;
using LedgeLink.Settlement.Worker.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddMongoDBClient("ledgelink");

// ── Service Bus Configuration ─────────────────────────────────────────────────
var serviceBusConnection = builder.Configuration["ServiceBus:ConnectionString"]
    ?? "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDeveloperTokenProvider=true";
builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection));

// DI wiring:
//   SettlementWorker (hosted)
//       └── SettleTradeService              (Application)
//               ├── ITradeSettlementRepository ← MongoTradeSettlementRepository (Infrastructure)
//               └── ISettlementPublisher       ← ServiceBusSettlementPublisher  (Infrastructure)
builder.Services.AddSingleton<ITradeSettlementRepository, MongoTradeSettlementRepository>();
builder.Services.AddSingleton<ServiceBusSettlementPublisher>();
builder.Services.AddSingleton<ISettlementPublisher>(sp => sp.GetRequiredService<ServiceBusSettlementPublisher>());
builder.Services.AddSingleton<SettleTradeService>();
builder.Services.AddHostedService<SettlementWorker>();

var host = builder.Build();
host.Run();
