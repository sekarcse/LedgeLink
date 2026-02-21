// LedgeLink.AppHost - Orchestrates all services via .NET Aspire
var builder = DistributedApplication.CreateBuilder(args);

// ── Infrastructure ──────────────────────────────────────────────────────────

// MongoDB: The shared ledger. All services connect to this.
var mongo = builder.AddMongoDB("mongo")
    .WithMongoExpress()
    .WithDataVolume("ledgelink-mongo-data");

var mongoDb = mongo.AddDatabase("ledgelink");

// Azure Service Bus Emulator - Aspire will handle this automatically
// Just reference it, don't try to configure connection strings manually
var messaging = builder.AddAzureServiceBus("messaging")
    .RunAsEmulator();

// ── Application Services ────────────────────────────────────────────────────

// 1. Distributor.API - Entry point (Hargreaves Lansdown)
var distributorApi = builder.AddProject<Projects.LedgeLink_Distributor_API>("distributor-api")
    .WithReference(mongoDb)
    .WithReference(messaging)
    .WithEnvironment("DISTRIBUTOR_NAME", "Hargreaves Lansdown")
    .WaitFor(mongoDb)
    .WaitFor(messaging);

// 2. Validator.Worker - Business rule validation
builder.AddProject<Projects.LedgeLink_Validator_Worker>("validator-worker")
    .WithReference(messaging)
    .WithEnvironment("WORKER_NAME", "Validator")
    .WaitFor(messaging)
    .WaitFor(distributorApi);

// 3. Settlement.Worker - Cryptographic seal + ledger write
builder.AddProject<Projects.LedgeLink_Settlement_Worker>("settlement-worker")
    .WithReference(mongoDb)
    .WithReference(messaging)
    .WithEnvironment("WORKER_NAME", "Settlement")
    .WaitFor(mongoDb)
    .WaitFor(messaging);

// 4. Participant.UI - Schroders view (Observer 1)
builder.AddProject<Projects.LedgeLink_Participant_UI>("participant-schroders")
    .WithReference(mongoDb)
    .WithEnvironment("PARTICIPANT_NAME", "Schroders")
    .WithEnvironment("PARTICIPANT_COLOR", "#1D4ED8")
    .WithEnvironment("PARTICIPANT_ROLE", "AssetManager")
    .WithEnvironment("PARTICIPANT_LOGO_INITIAL", "S")
    .WaitFor(mongoDb);

// 5. Participant.UI - Hargreaves view (Observer 2)
builder.AddProject<Projects.LedgeLink_Participant_UI>("participant-hargreaves")
    .WithReference(mongoDb)
    .WithEnvironment("PARTICIPANT_NAME", "Hargreaves Lansdown")
    .WithEnvironment("PARTICIPANT_COLOR", "#B91C1C")
    .WithEnvironment("PARTICIPANT_ROLE", "Distributor")
    .WithEnvironment("PARTICIPANT_LOGO_INITIAL", "H")
    .WaitFor(mongoDb);

builder.Build().Run();