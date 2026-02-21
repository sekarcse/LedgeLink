using Azure.Messaging.ServiceBus;
using LedgeLink.Participant.UI.Application.Services;
using LedgeLink.Participant.UI.Domain.Models;
using LedgeLink.Participant.UI.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── Service Bus ──────────────────────────────────────────────────────────────
var serviceBusConnectionString = builder.Configuration.GetConnectionString("messaging")
    ?? builder.Configuration["messaging"];

builder.Services.AddSingleton(new ServiceBusClient(
    serviceBusConnectionString,
    new ServiceBusClientOptions
    {
        TransportType = ServiceBusTransportType.AmqpTcp  // ← correct for emulator
    }));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Dependency Injection ─────────────────────────────────────────────────────
builder.Services.AddSingleton<TradeStreamService>();
builder.Services.AddHostedService<ServiceBusTradeListener>();

// ── Multi-tenant identity from environment variables ─────────────────────────
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    return new ParticipantContext
    {
        Name        = cfg["PARTICIPANT_NAME"]         ?? "Observer",
        Color       = cfg["PARTICIPANT_COLOR"]        ?? "#374151",
        Role        = cfg["PARTICIPANT_ROLE"]         ?? "Observer",
        LogoInitial = cfg["PARTICIPANT_LOGO_INITIAL"] ?? "P"
    };
});

var app = builder.Build();

app.MapDefaultEndpoints();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<LedgeLink.Participant.UI.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();