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

### 🚀 Ready to run: Simulation Mode
The application is configured to run out-of-the-box in **Simulation Mode**.
*   **No blockchain setup required** to start exploring.
*   The system will automatically detect the absence of credentials, log a warning, and generate simulated transaction hashes.
*   All UI features (links, status updates) and API endpoints will function exactly as they would in a live environment.

To move to a real testnet, see the [Live Blockchain Setup](#live-blockchain-setup) section below.

---

## Live Blockchain Setup (Optional)

To move from simulation to the real Ethereum Sepolia Testnet:

1.  **Get an RPC URL**: Sign up at [Infura](https://infura.io) or [Alchemy](https://alchemy.com) and copy your Sepolia URL.
2.  **Get a Private Key**: Export a private key from MetaMask (ensure it has some Sepolia ETH from a faucet).
3.  **Deploy the Contract**:
    *   Open `contracts/HashAnchor.sol` in [Remix IDE](https://remix.ethereum.org).
    *   Deploy to Sepolia and copy the **Contract Address**.
4.  **Configure LedgeLink**:
    Run these commands in the root directory:
    ```bash
    dotnet user-secrets set "Ethereum:RpcUrl" "YOUR_RPC_URL" --project LedgeLink.AppHost
    dotnet user-secrets set "Ethereum:PrivateKey" "YOUR_PRIVATE_KEY" --project LedgeLink.AppHost
    dotnet user-secrets set "Ethereum:ContractAddress" "YOUR_CONTRACT_ADDRESS" --project LedgeLink.AppHost
    ```

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

## Production Security & Privacy Considerations

While this Proof-of-Concept uses a public blockchain for anchoring, "real-world" financial deployments should consider the following:

1.  **Secret Management**: In this PoC, Ethereum private keys are passed via environment variables. For production, **Azure Key Vault** or **AWS Secrets Manager** should be used. Better yet, use **Managed Identities** to eliminate the need for long-lived credentials entirely.
2.  **Privacy & Data Sovereignty**: Even though only hashes are anchored, transaction metadata on public blockchains can be sensitive. For enterprise trade data, **Azure Confidential Ledger (ACL)** is the recommended alternative. It offers:
    *   **Tamper-proof storage** using Hardware Security Modules (HSMs).
    *   **High Performance** compared to public blockchain settlement times.
    *   **Privacy Control**: Access is restricted and managed, unlike public testnets.
3.  **Governance**: A production ledger requires a clear governance model for who can anchor and verify hashes, which can be implemented via Azure Confidential Ledger or a private/permissioned blockchain (e.g., Quorum).

---

## Phase 2: Pi4 K8s Deployment (Future)

The next phase moves the stack from local Aspire orchestration to a real multi-node K8s cluster running on Raspberry Pi 4 hardware.

### Checklist
- [ ] Set up K3s cluster on Pi4 nodes
- [ ] Swap Service Bus → Kafka in AppHost
- [ ] Build ARM64 Docker images
- [ ] Generate and apply K8s manifests via aspirate
- [ ] Configure persistent volumes for MongoDB on Pi4
