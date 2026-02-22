# LedgeLink — Real-Time Distributed Trade Settlement Platform

> A .NET Aspire proof-of-concept demonstrating real-time distributed trade settlement between financial institutions with **Blockchain Hash Anchoring** for immutable audit trails.

---

## What It Does

LedgeLink simulates a real-world trade settlement flow between two financial institutions — **Hargreaves Lansdown** (Distributor) and **Schroders** (Asset Manager). When a trade is submitted, it flows through a distributed pipeline, gets anchored to the Ethereum blockchain, and both parties see the result update live on their dashboards.

### Technical Flow

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

## Blockchain Hash Anchoring

After settlement, `Settlement.Worker` publishes a unique digital fingerprint of the trade to the Ethereum Sepolia testnet via a **Smart Contract**. This provides an **immutable on-chain receipt** that neither party can alter.

### How it Works (Layman's Terms)
*   **Who adds to the blockchain?** The Settlement Worker (the "Digital Notary").
*   **Where is it stored?** On the Ethereum Sepolia Testnet—a public, global network where data cannot be changed or deleted.
*   **Why?** If someone tries to tamper with the trade amount in the local MongoDB database, the system compares it with the fingerprint on the blockchain. If they don't match, the fraud is instantly detected.

### Smart Contract (Solidity)
```solidity
// HashAnchor.sol
mapping(string => bytes32) private _anchors;

function anchorHash(string id, bytes32 fingerprint) public {
    require(_anchors[id] == 0, "Already anchored!");
    _anchors[id] = fingerprint;
}
```

### Nethereum Integration (C#)
```csharp
public async Task<string> AnchorHashAsync(string orderId, string sha256Hash)
{
    var account = new Account(privateKey);
    var web3 = new Web3(account, rpcUrl);

    var txHash = await contract.GetFunction("anchorHash")
        .SendTransactionAsync(account.Address, orderId, sha256Hash);

    return txHash;
}
```

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

> **Note:** For blockchain anchoring to work, ensure `Ethereum__RpcUrl`, `Ethereum__PrivateKey`, and `Ethereum__ContractAddress` are configured in your environment or `appsettings.json`.

---

## Hash Verification (Double Integrity Check)

To verify a trade:
```
GET /api/trades/{externalOrderId}/verify
```

The system performs a **Double Integrity Check**:
1. **Local Check**: Recomputes the SHA-256 hash and compares it with the MongoDB record.
2. **On-Chain Check**: Retrieves the anchored hash from Ethereum and compares it with the local record.

**Result:**
> ✅ **DOUBLE VERIFIED** — Ledger and Blockchain both match.

---

## Project Structure

```
LedgeLink/
├── LedgeLink.AppHost/              # Aspire orchestration
├── LedgeLink.Shared/               # Domain models, HashService
├── LedgeLink.Distributor.API/      # Trade submission + Verification API
├── LedgeLink.Validator.Worker/     # Business rule validation
├── LedgeLink.Settlement.Worker/    # SHA-256 seal + Blockchain Anchoring
└── LedgeLink.Participant.UI/       # Blazor Server Dashboards (HL & Schroders)
```

---

## Phase 3: Pi4 K8s Deployment (Future)

The next phase moves the entire stack from local Aspire orchestration to a real multi-node K8s cluster running on Raspberry Pi 4 hardware.

### Checklist
- [x] Deploy simple hash-anchor smart contract to Sepolia
- [x] Add Nethereum to Settlement.Worker
- [x] Add `txHash` field to `TradeToken`
- [x] Update verify endpoint to check on-chain hash
- [x] Show Etherscan link in both Participant UIs
- [ ] Set up K3s cluster on Pi4 nodes
- [ ] Swap Service Bus → Kafka in AppHost
- [ ] Build ARM64 Docker images
- [ ] Generate and apply K8s manifests via aspirate
