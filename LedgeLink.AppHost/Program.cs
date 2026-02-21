// LedgeLink.AppHost - Orchestrates all services via .NET Aspire
var builder = DistributedApplication.CreateBuilder(args);

// ── Infrastructure ──────────────────────────────────────────────────────────

// MongoDB: The shared ledger. All services connect to this.
var mongo = builder.AddMongoDB("mongo")
    .WithEnvironment("MONGO_INITDB_ROOT_USERNAME", "root")
    .WithEnvironment("MONGO_INITDB_ROOT_PASSWORD", "example")
    .WithMongoExpress(express =>
        express.WithEnvironment("ME_CONFIG_MONGODB_ADMINUSERNAME", "root")
               .WithEnvironment("ME_CONFIG_MONGODB_ADMINPASSWORD", "example")
    )
    .WithDataVolume("ledgelink-mongo-data");

var mongoDb = mongo.AddDatabase("ledgelink");

// Service Bus Emulator Connection String
// This connection string points to the local Service Bus Emulator running in Docker
var serviceBusConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true";

// Add Service Bus as a connection string resource
var serviceBus = builder.AddConnectionString("messaging", serviceBusConnectionString);

// ── Application Services ────────────────────────────────────────────────────

// 1. Distributor.API - Entry point (Hargreaves Lansdown)
var distributorApi = builder.AddProject<Projects.LedgeLink_Distributor_API>("distributor-api")
    .WithReference(mongoDb)
    .WithReference(serviceBus)
    .WithEnvironment("DISTRIBUTOR_NAME", "Hargreaves Lansdown")
    .WithHttpEndpoint(port: 5100, name: "http")
    .WaitFor(mongoDb);

// 2. Validator.Worker - Business rule validation
builder.AddProject<Projects.LedgeLink_Validator_Worker>("validator-worker")
    .WithReference(serviceBus)
    .WithEnvironment("WORKER_NAME", "Validator")
    .WaitFor(distributorApi);

// 3. Settlement.Worker - Cryptographic seal + ledger write
builder.AddProject<Projects.LedgeLink_Settlement_Worker>("settlement-worker")
    .WithReference(mongoDb)
    .WithReference(serviceBus)
    .WithEnvironment("WORKER_NAME", "Settlement")
    .WaitFor(mongoDb);

// 4. Participant.UI - Schroders view (Observer 1)
builder.AddProject<Projects.LedgeLink_Participant_UI>("participant-schroders")
    .WithReference(mongoDb)
    .WithEnvironment("PARTICIPANT_NAME", "Schroders")
    .WithEnvironment("PARTICIPANT_COLOR", "#1D4ED8")
    .WithEnvironment("PARTICIPANT_ROLE", "AssetManager")
    .WithEnvironment("PARTICIPANT_LOGO_INITIAL", "S")
    .WithHttpEndpoint(port: 5200, name: "http")
    .WaitFor(mongoDb);

// 5. Participant.UI - Hargreaves view (Observer 2) - SAME binary, different env vars
builder.AddProject<Projects.LedgeLink_Participant_UI>("participant-hargreaves")
    .WithReference(mongoDb)
    .WithEnvironment("PARTICIPANT_NAME", "Hargreaves Lansdown")
    .WithEnvironment("PARTICIPANT_COLOR", "#B91C1C")
    .WithEnvironment("PARTICIPANT_ROLE", "Distributor")
    .WithEnvironment("PARTICIPANT_LOGO_INITIAL", "H")
    .WithHttpEndpoint(port: 5201, name: "http")
    .WaitFor(mongoDb);

builder.Build().Run();