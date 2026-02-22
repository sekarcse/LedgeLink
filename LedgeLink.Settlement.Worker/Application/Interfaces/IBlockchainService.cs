namespace LedgeLink.Settlement.Worker.Application.Interfaces;

/// <summary>
/// Application layer contract for blockchain operations.
/// </summary>
public interface IBlockchainService
{
    /// <summary>
    /// Anchors the cryptographic hash of a trade to the blockchain.
    /// Returns the transaction hash.
    /// </summary>
    Task<string> AnchorHashAsync(string externalOrderId, string sha256Hash, DateTime timestamp, CancellationToken ct = default);

    /// <summary>
    /// Verifies if a hash is anchored on-chain and matches the expected value.
    /// </summary>
    Task<(bool IsAnchored, string? AnchoredHash)> GetAnchoredHashAsync(string externalOrderId, CancellationToken ct = default);
}
