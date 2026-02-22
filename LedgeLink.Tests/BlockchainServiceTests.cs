using LedgeLink.Settlement.Worker.Infrastructure.Blockchain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LedgeLink.Tests;

public class BlockchainServiceTests
{
    [Fact]
    public async Task AnchorHashAsync_Simulates_WhenConfigMissing()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<NethereumBlockchainService>>();

        // Return null for config values
        mockConfig.Setup(c => c["Ethereum:RpcUrl"]).Returns((string)null);

        var service = new NethereumBlockchainService(mockConfig.Object, mockLogger.Object);

        // Act
        var txHash = await service.AnchorHashAsync("ORDER-1", "HASH-1", DateTime.UtcNow);

        // Assert
        Assert.StartsWith("0x", txHash);
        Assert.True(txHash.Length > 10);
    }
}
