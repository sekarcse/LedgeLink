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
        var raw  = $"{trade.ExternalOrderId}{trade.Amount:F2}{trade.Timestamp:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash); // uppercase hex e.g. "A1B2C3D4..."
    }

    public static bool VerifyHash(TradeToken trade)
    {
        if (string.IsNullOrEmpty(trade.SharedHash)) return false;
        return string.Equals(ComputeHash(trade), trade.SharedHash, StringComparison.OrdinalIgnoreCase);
    }
}
