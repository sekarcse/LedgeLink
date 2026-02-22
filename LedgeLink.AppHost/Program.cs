var builder = DistributedApplication.CreateBuilder(args);

// ── Infrastructure ───────────────────────────────────────────────────────────
var mongo = builder.AddMongoDB("mongo")
    .WithMongoExpress()
    .WithDataVolume("ledgelink-mongo-data");

var mongoDb = mongo.AddDatabase("ledgelink");

var messaging = builder.AddAzureServiceBus("messaging")
    .RunAsEmulator(emulator =>
    {
        emulator.WithConfigurationFile("./ServiceBusEmulator/config.json");
    });

// ── Application Services ─────────────────────────────────────────────────────

// 1. Distributor.API
var distributorApi = builder.AddProject<Projects.LedgeLink_Distributor_API>("distributor-api")
    .WithReference(mongoDb)
    .WithReference(messaging)
    .WithEnvironment("DISTRIBUTOR_NAME", "Hargreaves Lansdown")
    .WithEnvironment("Ethereum__RpcUrl", builder.Configuration["Ethereum:RpcUrl"] ?? "")
    .WithEnvironment("Ethereum__ContractAddress", builder.Configuration["Ethereum:ContractAddress"] ?? "")
    .WithEnvironment("Ethereum__BlockExplorerUrl", builder.Configuration["Ethereum:BlockExplorerUrl"] ?? "")
    .WaitFor(mongoDb)
    .WaitFor(messaging);

// 2. Validator.Worker
builder.AddProject<Projects.LedgeLink_Validator_Worker>("validator-worker")
    .WithReference(messaging)
    .WithEnvironment("WORKER_NAME", "Validator")
    .WaitFor(messaging)
    .WaitFor(distributorApi);

// 3. Settlement.Worker
builder.AddProject<Projects.LedgeLink_Settlement_Worker>("settlement-worker")
    .WithReference(mongoDb)
    .WithReference(messaging)
    .WithEnvironment("WORKER_NAME", "Settlement")
    .WithEnvironment("Ethereum__RpcUrl", builder.Configuration["Ethereum:RpcUrl"] ?? "")
    .WithEnvironment("Ethereum__PrivateKey", builder.Configuration["Ethereum:PrivateKey"] ?? "")
    .WithEnvironment("Ethereum__ContractAddress", builder.Configuration["Ethereum:ContractAddress"] ?? "")
    .WaitFor(mongoDb)
    .WaitFor(messaging);

// 4. Participant.UI - Schroders (no MongoDB reference anymore)
builder.AddProject<Projects.LedgeLink_Participant_UI>("participant-schroders")
    .WithReference(messaging)
    .WithEnvironment("PARTICIPANT_NAME", "Schroders")
    .WithEnvironment("PARTICIPANT_COLOR", "#1D4ED8")
    .WithEnvironment("Ethereum__BlockExplorerUrl", builder.Configuration["Ethereum:BlockExplorerUrl"] ?? "")
    .WithEnvironment("PARTICIPANT_ROLE", "AssetManager")
    .WithEnvironment("PARTICIPANT_LOGO_INITIAL", "S")
    .WithEnvironment("SERVICEBUS_SUBSCRIPTION", "schroders")
    .WithHttpEndpoint(port: 5201, name: "schroders-http") 
    .WaitFor(messaging);

// 5. Participant.UI - Hargreaves (no MongoDB reference anymore)
builder.AddProject<Projects.LedgeLink_Participant_UI>("participant-hargreaves")
    .WithReference(messaging)
    .WithEnvironment("PARTICIPANT_NAME", "Hargreaves Lansdown")
    .WithEnvironment("PARTICIPANT_COLOR", "#B91C1C")
    .WithEnvironment("Ethereum__BlockExplorerUrl", builder.Configuration["Ethereum:BlockExplorerUrl"] ?? "")
    .WithEnvironment("PARTICIPANT_ROLE", "Distributor")
    .WithEnvironment("PARTICIPANT_LOGO_INITIAL", "H")
    .WithEnvironment("SERVICEBUS_SUBSCRIPTION", "hargreaveslansdown")
    .WithHttpEndpoint(port: 5200, name: "hargreaves-http") 
    .WaitFor(messaging);

builder.Build().Run();