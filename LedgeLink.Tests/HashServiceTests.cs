using LedgeLink.Shared.Application.Services;
using LedgeLink.Shared.Domain.Models;
using Xunit;

namespace LedgeLink.Tests;

public class HashServiceTests
{
    [Fact]
    public void ComputeHash_ReturnsConsistentHash()
    {
        // Arrange
        var trade = new TradeToken
        {
            ExternalOrderId = "TEST-123",
            Amount = 100.50m,
            Timestamp = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var hash1 = HashService.ComputeHash(trade);
        var hash2 = HashService.ComputeHash(trade);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.False(string.IsNullOrEmpty(hash1));
    }

    [Fact]
    public void VerifyHash_ReturnsTrueForOriginalTrade()
    {
        // Arrange
        var trade = new TradeToken
        {
            ExternalOrderId = "TEST-123",
            Amount = 100.50m,
            Timestamp = DateTime.UtcNow
        };
        trade.SharedHash = HashService.ComputeHash(trade);

        // Act
        var isValid = HashService.VerifyHash(trade);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyHash_ReturnsFalseIfAmountChanged()
    {
        // Arrange
        var trade = new TradeToken
        {
            ExternalOrderId = "TEST-123",
            Amount = 100.50m,
            Timestamp = DateTime.UtcNow
        };
        trade.SharedHash = HashService.ComputeHash(trade);

        // Tamper
        var tamperedTrade = new TradeToken
        {
            InternalId = trade.InternalId,
            ExternalOrderId = trade.ExternalOrderId,
            Amount = 100.51m, // Changed
            Timestamp = trade.Timestamp,
            SharedHash = trade.SharedHash
        };

        // Act
        var isValid = HashService.VerifyHash(tamperedTrade);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void BlockchainHash_IsConsistentWithCompositeFields()
    {
        // Arrange
        var trade = new TradeToken
        {
            ExternalOrderId = "ORDER-1",
            SharedHash = "SHA256-HASH",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var hash1 = BlockchainHashService.ComputeAnchoredHashHex(trade.ExternalOrderId, trade.SharedHash, trade.Timestamp);
        var hash2 = BlockchainHashService.ComputeAnchoredHashHex(trade.ExternalOrderId, trade.SharedHash, trade.Timestamp);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.StartsWith("0x", hash1);
    }
}
