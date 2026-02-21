using System.Security.Cryptography;
using System.Text;
using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Shared.Application.Services;

/// <summary>
/// Domain service: computes and verifies the SHA-256 cryptographic seal.
///
/// Formula: SHA256( ExternalOrderId + Amount.ToString("F2") + Timestamp.ISO8601 )
///
/// Pure static — no I/O, no dependencies. Safe to call from any layer.
/// Immutability guarantee: post-settlement tampering with any of the three
/// input fields causes VerifyHash() to return false.
/// </summary>
public static class HashService
{
    public static string ComputeHash(TradeToken trade)
    {
        // Truncate to milliseconds to match MongoDB's storage precision
        var timestamp = new DateTime(
            trade.Timestamp.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond,
            DateTimeKind.Utc);

        var raw = $"{trade.ExternalOrderId}{trade.Amount:F2}{timestamp:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    public static bool VerifyHash(TradeToken trade)
    {
        if (string.IsNullOrEmpty(trade.SharedHash)) return false;
        return string.Equals(ComputeHash(trade), trade.SharedHash, StringComparison.OrdinalIgnoreCase);
    }
}
