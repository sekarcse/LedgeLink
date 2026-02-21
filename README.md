# LedgeLink — Real-Time Distributed Trade Settlement Platform

> A .NET Aspire proof-of-concept demonstrating real-time distributed trade settlement between financial institutions using Azure Service Bus, MongoDB, and Blazor Server.

---

## What It Does

LedgeLink simulates a real-world trade settlement flow between two financial institutions — **Hargreaves Lansdown** (Distributor) and **Schroders** (Asset Manager). When a trade is submitted, it flows through a distributed pipeline and both parties see the result update live on their dashboards within 1–2 seconds — no page refresh required.

```
[Swagger / curl]
      │
      ▼ POST /api/trades
┌─────────────────────┐
│  Distributor.API    │  ── Writes Pending to MongoDB
│  (Hargreaves HL)    │  ── Publishes to Service Bus: trade.requested
└─────────────────────┘
      │
      ▼ Service Bus: trade.requested
┌─────────────────────┐
│  Validator.Worker   │  ── Checks business rules
│                     │  ── Publishes: trade.validated / trade.rejected
└─────────────────────┘
      │
      ▼ Service Bus: trade.validated
┌─────────────────────┐
│  Settlement.Worker  │  ── Computes SHA-256 hash
│                     │  ── Updates MongoDB: Status=Settled, SharedHash
│                     │  ── Publishes to topic: trade.settled
└─────────────────────┘
      │
      ▼ Service Bus Topic: trade.settled (2 subscriptions)
┌────────────┐   ┌────────────┐
│ UI:        │   │ UI:        │
│ Schroders  │   │ Hargreaves │  ◄─── Both update SIMULTANEOUSLY
└────────────┘   └────────────┘
```

---

## Architecture Decisions

**Why Azure Service Bus instead of RabbitMQ?**
Service Bus emulator runs natively via .NET Aspire with zero Docker config overhead, and maps directly to a real Azure deployment.

**Why Service Bus topic for settlement notifications instead of MongoDB Change Streams?**
Change Streams require a MongoDB replica set, which adds significant local dev complexity. The Service Bus topic pattern is simpler, more reliable, and production-ready — each participant UI subscribes to its own subscription on the `trade.settled` topic.

**Why are Participant UIs Aspire-unaware?**
Services only reference plain SDKs (`MongoDB.Driver`, `Azure.Messaging.ServiceBus`). Aspire injects connection strings via environment variables at runtime through `WithReference()` in the AppHost. This means services run identically in local dev and production.

---

## Prerequisites

| Tool | Version | Install |
|---|---|---|
| .NET SDK | 9.0+ | https://dot.net/get-dotnet-9 |
| .NET Aspire workload | 9.1+ | `dotnet workload install aspire` |
| Docker Desktop | Latest | https://www.docker.com/products/docker-desktop/ |

**Verify your setup:**
```bash
dotnet --version        # Should be 9.x
dotnet workload list    # Should show: aspire
docker --version        # Docker must be running
```

---

## Quick Start

```bash
# 1. Clone the repo
git clone https://github.com/sekarcse/LedgeLink.git
cd LedgeLink

# 2. Build the solution
dotnet build LedgeLink.sln

# 3. Run the Aspire AppHost — starts ALL services automatically
dotnet run --project LedgeLink.AppHost
```

> **Important:** Always run `dotnet build` before starting the AppHost. Aspire runs services with `--no-build` and will use stale binaries if you skip this step.

---

## What Opens Automatically

| URL | Service |
|---|---|
| http://localhost:18888 | **Aspire Dashboard** — logs, traces, health for all services |
| http://localhost:5100 | **Distributor API** (Swagger UI) |
| http://localhost:5200 | **Participant UI — Hargreaves Lansdown** (red theme) |
| http://localhost:5201 | **Participant UI — Schroders** (blue theme) |

> Ports for MongoDB and Service Bus are assigned dynamically by Aspire — check the dashboard Resources tab for the exact values.

---

## Testing the Full Flow

### Step 1 — Submit a trade

Open http://localhost:5100 (Swagger), expand `POST /api/trades`, click **Try it out**:

```json
{
  "externalOrderId": "HL-998877",
  "amount": 50000.00,
  "assetManager": "Schroders"
}
```

Click **Execute**.

### Step 2 — Watch it settle in real time

Open both participant dashboards side by side:
- http://localhost:5200 (Hargreaves — red)
- http://localhost:5201 (Schroders — blue)

You will see the trade appear as **Pending**, flip to **Validated**, then **Settled** with a SHA-256 hash — all within 1–2 seconds, with no page refresh.

### Step 3 — Test idempotency

Submit the **same** `externalOrderId` again. The API returns HTTP 200:

> "Duplicate order detected. Returning original trade record."

No duplicate is created in MongoDB.

### Step 4 — Test rejection

Submit a trade with `amount: -100`. The API immediately returns HTTP 400:

> "Amount must be greater than zero."

---

## Project Structure

```
LedgeLink/
├── LedgeLink.AppHost/              # Aspire orchestration — run this
├── LedgeLink.ServiceDefaults/      # Shared Aspire telemetry/health wiring
├── LedgeLink.Shared/               # Domain models, HashService, QueueNames
│   ├── Domain/Models/TradeToken.cs
│   ├── Domain/Models/TradeStatus.cs
│   ├── Application/Constants/QueueNames.cs
│   └── Application/Services/HashService.cs
├── LedgeLink.Distributor.API/      # ASP.NET Core 9 Web API
├── LedgeLink.Validator.Worker/     # BackgroundService — business rule validation
├── LedgeLink.Settlement.Worker/    # BackgroundService — SHA-256 seal + MongoDB write
└── LedgeLink.Participant.UI/       # Blazor Server — deployed twice with different config
```

---

## Service Bus Topology

The emulator is configured via `LedgeLink.AppHost/ServiceBusEmulator/config.json`:

| Type | Name | Purpose |
|---|---|---|
| Queue | `trade.requested` | Distributor → Validator |
| Queue | `trade.validated` | Validator → Settlement |
| Queue | `trade.rejected` | Validator → (future: notification) |
| Topic | `trade.settled` | Settlement → both Participant UIs |
| Subscription | `schroders` | Schroders UI receives settled trades |
| Subscription | `hargreaveslansdown` | Hargreaves UI receives settled trades |

---

## Hash Verification (Immutability Demo)

The Settlement.Worker computes:

```
SHA256(ExternalOrderId + Amount.ToString("F2") + Timestamp.ISO8601)
```

To verify a hash in C#:

```csharp
using LedgeLink.Shared.Application.Services;

var isValid = HashService.VerifyHash(trade); // false if MongoDB record was tampered
```

If someone modifies `Amount` directly in MongoDB after settlement, `VerifyHash()` returns `false` — proving the record was tampered with.

---

## Key Package Versions

| Package | Version | Where |
|---|---|---|
| `MongoDB.Driver` | 2.28.0 | Distributor.API, Settlement.Worker, Participant.UI |
| `MongoDB.Bson` | 2.28.0 | LedgeLink.Shared |
| `Azure.Messaging.ServiceBus` | 7.18.4 | Distributor.API, Validator.Worker, Settlement.Worker, Participant.UI |

> **Note:** Do not upgrade `MongoDB.Bson` to 3.x independently — it must match `MongoDB.Driver`. The `Aspire.MongoDB.Driver` and `Aspire.Azure.Messaging.ServiceBus` packages are intentionally **not** used in service projects; Aspire injects connection strings via environment variables, and services use plain SDKs.

---

## Troubleshooting

| Issue | Fix |
|---|---|
| Workers crash on startup | Run `dotnet build LedgeLink.sln` first, then restart |
| Service Bus emulator fails to start | Check `config.json` — namespace name must be `sbemulatorns` |
| MongoDB connection fails | Wait 10–15s for container to initialise |
| Both Participant UIs on same port | Check Aspire dashboard for dynamic port assignment |
| Docker not running | Start Docker Desktop before running AppHost |

---

## Phase 2: Blockchain Hash Anchoring + Pi4 K8s Deployment

### Overview

Phase 2 extends LedgeLink with two major additions:

1. **Blockchain hash anchoring** — after settlement, the SHA-256 hash is published to Ethereum Sepolia testnet as an immutable audit trail. No one can dispute the settled hash because it lives on-chain.
2. **Pi4 Kubernetes deployment** — the entire stack moves from local Aspire orchestration to a real multi-node K8s cluster running on Raspberry Pi 4 hardware.

---

### Phase 2 Architecture

```
[Swagger / curl]
      │
      ▼ POST /api/trades
┌─────────────────────┐
│  Distributor.API    │  ── Writes Pending to MongoDB
│  (Hargreaves HL)    │  ── Publishes to Kafka: trade.requested
└─────────────────────┘
      │
      ▼ Kafka: trade.requested
┌─────────────────────┐
│  Validator.Worker   │  ── Checks business rules
│                     │  ── Publishes: trade.validated / trade.rejected
└─────────────────────┘
      │
      ▼ Kafka: trade.validated
┌─────────────────────┐
│  Settlement.Worker  │  ── Computes SHA-256 hash
│                     │  ── Updates MongoDB: Status=Settled
│                     │  ── Anchors hash to Ethereum Sepolia  ← NEW
│                     │  ── Publishes to topic: trade.settled
└─────────────────────┘
      │
      ├──────────────────────────────────────┐
      ▼                                      ▼
┌────────────┐   ┌────────────┐    ┌─────────────────────┐
│ UI:        │   │ UI:        │    │  Ethereum Sepolia   │
│ Schroders  │   │ Hargreaves │    │  TX Hash on-chain   │
│            │   │            │    │  (immutable truth)  │
└────────────┘   └────────────┘    └─────────────────────┘
```

---

### Blockchain Hash Anchoring

After settlement, `Settlement.Worker` publishes the SHA-256 hash to Ethereum Sepolia via a simple smart contract. This gives every settled trade an **immutable on-chain receipt** that neither party can alter.

**What gets anchored:**
```
keccak256(externalOrderId + sha256Hash + timestamp)
→ stored on Sepolia → TX hash returned
→ stored in MongoDB as txHash
→ shown in both UIs with Etherscan link
```

**What you need:**
- Infura or Alchemy free Sepolia RPC endpoint
- A funded Sepolia wallet (free ETH from faucet.sepolia.dev)
- Nethereum NuGet package in Settlement.Worker

**Verify a trade on-chain:**
```
GET /api/trades/{externalOrderId}/verify
→ returns: isValid, storedHash, txHash, etherscanUrl
```

Any post-settlement tampering in MongoDB will cause the recomputed hash to differ from the on-chain anchored hash — **cryptographic proof of fraud**.

---

### Kafka Swap (Service Bus → Kafka)

On Pi4, replace Azure Service Bus with Kafka for lower latency and no cloud dependency:

```csharp
// AppHost/Program.cs — replace
var messaging = builder.AddAzureServiceBus("messaging").RunAsEmulator();

// with
var messaging = builder.AddKafka("messaging");
```

Update worker packages:
```xml
<!-- Remove -->
<PackageReference Include="Azure.Messaging.ServiceBus" Version="7.18.4" />

<!-- Add -->
<PackageReference Include="Confluent.Kafka" Version="2.x.x" />
```

---

### Pi4 K8s Deployment

**Prerequisites on Pi4 cluster:**
```bash
# Each Pi4 node needs
curl -sfL https://get.k3s.io | sh -   # lightweight K8s for ARM
docker --version                        # Docker must be running
```

**Generate K8s manifests from Aspire:**
```bash
# Install aspirate
dotnet tool install -g aspirate

# Generate manifests
aspirate generate --project-path LedgeLink.AppHost

# Review output
ls ./aspirate-output/
```

**Deploy to Pi4 cluster:**
```bash
# Set your Pi4 kubeconfig
export KUBECONFIG=~/.kube/pi4-config

# Deploy everything
kubectl apply -f ./aspirate-output/

# Watch rollout
kubectl get pods -w -n ledgelink
```

**Expected pods:**
```
NAME                          READY   STATUS
distributor-api-xxx           1/1     Running
validator-worker-xxx          1/1     Running
settlement-worker-xxx         1/1     Running
participant-schroders-xxx     1/1     Running
participant-hargreaves-xxx    1/1     Running
mongo-xxx                     1/1     Running
kafka-xxx                     1/1     Running
```

**ARM64 image builds** (Pi4 is ARM architecture):
```bash
# Build multi-arch images
docker buildx build --platform linux/arm64 \
  -t ledgelink/distributor-api:latest \
  ./LedgeLink.Distributor.API

# Repeat for each service
```

---

### Phase 2 Checklist

**Blockchain anchoring:**
- [ ] Deploy simple hash-anchor smart contract to Sepolia
- [ ] Add Nethereum to Settlement.Worker
- [ ] Add `txHash` field to `TradeToken`
- [ ] Update verify endpoint to check on-chain hash
- [ ] Show Etherscan link in both Participant UIs

**Pi4 K8s:**
- [ ] Set up K3s cluster on Pi4 nodes
- [ ] Swap Service Bus → Kafka in AppHost
- [ ] Build ARM64 Docker images
- [ ] Generate and apply K8s manifests via aspirate
- [ ] Configure persistent volumes for MongoDB on Pi4
- [ ] Set up ingress for UI endpoints