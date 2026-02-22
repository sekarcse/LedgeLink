# LedgeLink — Real-Time Distributed Trade Settlement Platform

> A .NET Aspire proof-of-concept demonstrating real-time distributed trade settlement between financial institutions with **Blockchain Hash Anchoring** for immutable audit trails.

---

## What It Does

LedgeLink simulates a real-world trade settlement flow between two financial institutions — **Hargreaves Lansdown** (Distributor) and **Schroders** (Asset Manager). When a trade is submitted, it flows through a distributed pipeline, gets anchored to the Ethereum blockchain, and both parties see the result update live on their dashboards.

### Technical Flow Chart

```text
[ USER/SYSTEM ]
      │ (POST /api/trades)
      ▼
1. [ Distributor.API ] ────► Saves to [ MongoDB ] (Status: Pending)
      │                ────► Sends to [ Service Bus ] (trade.requested)
      ▼
2. [ Validator.Worker ] ───► Validates rules
      │                ───► Sends to [ Service Bus ] (trade.validated)
      ▼
3. [ Settlement.Worker ] <── RECEIVES Validated Trade
      │
      ├─► A. Computes SHA-256 Fingerprint (The "Seal")
      ├─► B. ANCHORS Fingerprint to [ Ethereum Blockchain ]
      ├─► C. Receives "Tx Hash" (The permanent receipt)
      └─► D. UPDATES [ MongoDB ] (Status: Settled + TxHash)
      │
      ▼
4. [ Participant UIs ]  ◄─── PUSH Notification (trade.settled)
                            Users see "✅ Settled" and click "⛓️ View Tx"
                            to verify the proof on Etherscan.
```

---

## Blockchain Hash Anchoring (Layman's Terms)

*   **Who adds to the blockchain?** The Settlement Worker (acting as a "Digital Notary").
*   **Where is it stored?** On the Ethereum Sepolia Testnet—a public, global network where data is immutable (it cannot be changed or deleted by anyone).
*   **How does it get updated?** A **Smart Contract** records the trade's unique digital fingerprint (hash).
*   **Why?** If someone tampered with the trade amount in the local database after settlement, the system would detect that the fingerprint no longer matches the one anchored on the blockchain.

### Example: Smart Contract Logic (Solidity)
```solidity
mapping(string => bytes32) private _anchors;

function anchorHash(string id, bytes32 fingerprint) public {
    require(_anchors[id] == 0, "Already anchored!");
    _anchors[id] = fingerprint;
}
```

---

## Prerequisites

| Tool | Version | Install |
|---|---|---|
| .NET SDK | 9.0+ | https://dot.net/get-dotnet-9 |
| .NET Aspire workload | 9.1+ | `dotnet workload install aspire` |
| Docker Desktop | Latest | https://www.docker.com/products/docker-desktop/ |

---

## Quick Start

```bash
# 1. Clone the repo
git clone https://github.com/sekarcse/LedgeLink.git
cd LedgeLink

# 2. Build the solution
dotnet build LedgeLink.sln

# 3. Run the Aspire AppHost
dotnet run --project LedgeLink.AppHost
```

> **Note:** For blockchain anchoring, ensure `Ethereum__RpcUrl`, `Ethereum__PrivateKey`, and `Ethereum__ContractAddress` are configured in your environment.

---

## Integrity Verification

To verify a trade's integrity:
```
GET /api/trades/{externalOrderId}/verify
```

The system performs a **Double Integrity Check**:
1. **Local Check**: Recomputes the SHA-256 hash and compares it with the database record.
2. **On-Chain Check**: Compares the database record with the anchored hash on Ethereum.

---

## Project Structure

```
LedgeLink/
├── LedgeLink.AppHost/              # Aspire orchestration
├── LedgeLink.Shared/               # Domain models & Hash logic
├── LedgeLink.Distributor.API/      # Trade submission & Verification
├── LedgeLink.Validator.Worker/     # Business rule validation
├── LedgeLink.Settlement.Worker/    # SHA-256 seal & Blockchain Anchoring
└── LedgeLink.Participant.UI/       # Blazor Server Dashboards
```

---

## Phase 2: Pi4 K8s Deployment (Future)

The next phase moves the stack from local Aspire orchestration to a real multi-node K8s cluster running on Raspberry Pi 4 hardware.

### Checklist
- [ ] Set up K3s cluster on Pi4 nodes
- [ ] Swap Service Bus → Kafka in AppHost
- [ ] Build ARM64 Docker images
- [ ] Generate and apply K8s manifests via aspirate
- [ ] Configure persistent volumes for MongoDB on Pi4
