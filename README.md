# LedgeLink — Real-Time Distributed Trade Settlement Platform
## Phase 1: .NET Aspire Local POC

---

## Prerequisites

| Tool | Version | Install |
|------|---------|---------|
| .NET SDK | 9.0+ | https://dot.net/get-dotnet-9 |
| .NET Aspire workload | 9.1+ | `dotnet workload install aspire` |
| Docker Desktop | Latest | https://www.docker.com/products/docker-desktop/ |

**Verify your setup:**
```bash
dotnet --version          # Should be 9.x
dotnet workload list      # Should show: aspire
docker --version          # Docker must be running
```

---

## Quick Start (3 commands)

```bash
# 1. Clone / open the solution folder
cd LedgeLink

# 2. Restore all NuGet packages
dotnet restore LedgeLink.sln

# 3. Run the Aspire AppHost — starts ALL containers automatically
dotnet run --project LedgeLink.AppHost
```

That's it. Aspire will pull MongoDB and RabbitMQ Docker images automatically.

---

## What Opens Automatically

| URL | Service |
|-----|---------|
| http://localhost:15888 | **Aspire Dashboard** — logs, traces, health for all services |
| http://localhost:5100 | **Distributor.API** (Swagger UI at root) |
| http://localhost:5200 | **Participant UI — Schroders** (blue theme) |
| http://localhost:5201 | **Participant UI — Hargreaves** (red theme) |
| http://localhost:15672 | **RabbitMQ Management** (guest / guest) |
| http://localhost:8081 | **Mongo Express** (MongoDB UI) |

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

### Step 2 — Watch it settle in real-time
Open **both** participant dashboards side-by-side:
- http://localhost:5200 (Schroders — blue)
- http://localhost:5201 (Hargreaves — red)

You will see the trade appear as **Pending**, flip to **Validated**, then **Settled** with a SHA-256 hash — all within 1-2 seconds, with no page refresh.

### Step 3 — Test idempotency
Submit the **same** `externalOrderId` again. The API returns HTTP 200 with the message:
> "Duplicate order detected. Returning original trade record."

No duplicate is created in MongoDB.

### Step 4 — Test rejection
Submit a trade with `amount: -100`. The Validator.Worker rejects it and you will see:
> Status: Rejected ❌

---

## Architecture at a Glance

```
[Swagger / curl]
      │
      ▼ POST /api/trades
┌─────────────────────┐
│  Distributor.API    │  ── Writes Pending to MongoDB
│  (Hargreaves HL)    │  ── Publishes to RabbitMQ: trade.requested
└─────────────────────┘
      │
      ▼ RabbitMQ: trade.requested
┌─────────────────────┐
│  Validator.Worker   │  ── Checks business rules
│                     │  ── Publishes: trade.validated / trade.rejected
└─────────────────────┘
      │
      ▼ RabbitMQ: trade.validated
┌─────────────────────┐
│  Settlement.Worker  │  ── Computes SHA-256 hash
│                     │  ── Updates MongoDB: Status=Settled, SharedHash
└─────────────────────┘
      │
      ▼ MongoDB Change Stream (push — no polling)
┌────────────┐   ┌────────────┐
│ UI:        │   │ UI:        │
│ Schroders  │   │ Hargreaves │  ◄─── Both update SIMULTANEOUSLY
└────────────┘   └────────────┘
```

---

## Project Structure

```
LedgeLink/
├── LedgeLink.AppHost/           # Aspire orchestration — run this
├── LedgeLink.ServiceDefaults/   # Shared Aspire telemetry/health wiring
├── LedgeLink.Shared/            # Models, HashService, QueueNames
│   ├── Models/TradeToken.cs
│   ├── Models/TradeStatus.cs
│   ├── Constants/QueueNames.cs
│   └── Services/HashService.cs
├── LedgeLink.Distributor.API/   # ASP.NET Core 9 Web API
├── LedgeLink.Validator.Worker/  # BackgroundService
├── LedgeLink.Settlement.Worker/ # BackgroundService
└── LedgeLink.Participant.UI/    # Blazor Server (deployed twice)
```

---

## Hash Verification (Immutability Demo)

The Settlement.Worker computes:
```
SHA256(ExternalOrderId + Amount.ToString("F2") + Timestamp.ISO8601)
```

To verify a hash manually in C#:
```csharp
using LedgeLink.Shared.Services;

var isValid = HashService.VerifyHash(trade); // true if untampered
```

If someone modifies `Amount` directly in MongoDB after settlement, `VerifyHash()` returns `false` — proving the record was tampered with.

---

## Phase 2: Pi4 K8s Deployment (Next Steps)

```bash
# Install aspirate
dotnet tool install -g aspirate

# Generate K8s manifests from AppHost
aspirate generate --project-path LedgeLink.AppHost

# Deploy to Pi4 cluster
kubectl apply -f ./aspirate-output/
```

Kafka swap: Replace `AddRabbitMQ()` with `AddKafka()` in AppHost and update the worker 
packages from `Aspire.RabbitMQ.Client` to `Confluent.Kafka`.

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Docker not running | Start Docker Desktop before running AppHost |
| Port already in use | Change ports in AppHost Program.cs |
| MongoDB connection fail | Wait 10-15s for Docker container to initialise |
| RabbitMQ not ready | Workers auto-retry — give it 30 seconds |
| Aspire workload missing | `dotnet workload install aspire` |
