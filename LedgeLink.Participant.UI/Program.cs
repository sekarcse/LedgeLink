using LedgeLink.Participant.UI.Application.Interfaces;
using LedgeLink.Participant.UI.Application.Services;
using LedgeLink.Participant.UI.Domain.Models;
using LedgeLink.Participant.UI.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddMongoDBClient("ledgelink");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Dependency Inversion ─────────────────────────────────────────────────────
//   Dashboard.razor
//       └── TradeStreamService            (Application)
//               └── ITradeStreamRepository ← MongoTradeStreamRepository (Infrastructure)
builder.Services.AddSingleton<ITradeStreamRepository, MongoTradeStreamRepository>();
builder.Services.AddSingleton<TradeStreamService>();

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

// Start the Change Stream background loop
var stream = app.Services.GetRequiredService<TradeStreamService>();
_ = stream.StartAsync(app.Lifetime.ApplicationStopping);

app.Run();
