using System.Text;
using Nethereum.Util;

namespace LedgeLink.Shared.Application.Services;

/// <summary>
/// Domain service: computes the composite Keccak256 hash anchored to the blockchain.
///
/// Formula: Keccak256( ExternalOrderId + SHA256Hash + Timestamp.ISO8601 )
/// </summary>
public static class BlockchainHashService
{
    public static byte[] ComputeAnchoredHash(string externalOrderId, string sha256Hash, DateTime timestamp)
    {
        // Truncate to milliseconds to match MongoDB's storage precision
        var preciseTimestamp = new DateTime(
            timestamp.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond,
            DateTimeKind.Utc);

        var raw = $"{externalOrderId}{sha256Hash}{preciseTimestamp:O}";
        var bytes = Encoding.UTF8.GetBytes(raw);
        return Sha3Keccack.Current.CalculateHash(bytes);
    }

    public static string ComputeAnchoredHashHex(string externalOrderId, string sha256Hash, DateTime timestamp)
    {
        var hash = ComputeAnchoredHash(externalOrderId, sha256Hash, timestamp);
        return "0x" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
