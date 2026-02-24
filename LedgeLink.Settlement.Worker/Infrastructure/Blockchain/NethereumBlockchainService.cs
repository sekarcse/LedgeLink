using LedgeLink.Settlement.Worker.Application.Interfaces;
using LedgeLink.Shared.Application.Services;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Text;

namespace LedgeLink.Settlement.Worker.Infrastructure.Blockchain;

public sealed class NethereumBlockchainService : IBlockchainService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<NethereumBlockchainService> _logger;
    private readonly string? _rpcUrl;
    private readonly string? _privateKey;
    private readonly string? _contractAddress;

    public NethereumBlockchainService(IConfiguration configuration, ILogger<NethereumBlockchainService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _rpcUrl = configuration["Ethereum:RpcUrl"];
        _privateKey = configuration["Ethereum:PrivateKey"];
        _contractAddress = configuration["Ethereum:ContractAddress"];
    }

    public async Task<string> AnchorHashAsync(string externalOrderId, string sha256Hash, DateTime timestamp, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_rpcUrl) || string.IsNullOrEmpty(_privateKey) || string.IsNullOrEmpty(_contractAddress))
        {
            _logger.LogWarning("Blockchain configuration missing. Simulating anchoring for {ExternalOrderId}", externalOrderId);
            return "0x" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        }

        try
        {
            var account = new Account(_privateKey);
            var web3 = new Web3(account, _rpcUrl);

            var anchoredHash = BlockchainHashService.ComputeAnchoredHash(externalOrderId, sha256Hash, timestamp);

            var contract = web3.Eth.GetContract(GetAbi(), _contractAddress);
            var anchorFunction = contract.GetFunction("anchorHash");

            // Use estimated gas if possible, but for simplicity we can just send.
            // Nethereum handles gas price automatically.
            var txHash = await anchorFunction.SendTransactionAsync(
                account.Address,
                null, // Gas
                null, // Value
                externalOrderId,
                anchoredHash);

            _logger.LogInformation("Successfully anchored hash for {ExternalOrderId}. Tx: {TxHash}", externalOrderId, txHash);
            return txHash;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to anchor hash for {ExternalOrderId} to blockchain.", externalOrderId);
            throw;
        }
    }

    public async Task<(bool IsAnchored, string? AnchoredHash)> GetAnchoredHashAsync(string externalOrderId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_rpcUrl) || string.IsNullOrEmpty(_contractAddress))
        {
            return (false, null);
        }

        try
        {
            var web3 = new Web3(_rpcUrl);
            var contract = web3.Eth.GetContract(GetAbi(), _contractAddress);
            var getAnchorFunction = contract.GetFunction("getAnchor");

            var result = await getAnchorFunction.CallAsync<byte[]>(externalOrderId);

            if (result == null || result.All(b => b == 0))
            {
                return (false, null);
            }

            return (true, result.ToHex(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve anchored hash for {ExternalOrderId} from blockchain.", externalOrderId);
            return (false, null);
        }
    }

    private string GetAbi()
    {
        return @"[
            {
                ""inputs"": [
                    { ""internalType"": ""string"", ""name"": ""externalOrderId"", ""type"": ""string"" },
                    { ""internalType"": ""bytes32"", ""name"": ""anchoredHash"", ""type"": ""bytes32"" }
                ],
                ""name"": ""anchorHash"",
                ""outputs"": [],
                ""stateMutability"": ""nonpayable"",
                ""type"": ""function""
            },
            {
                ""inputs"": [
                    { ""internalType"": ""string"", ""name"": ""externalOrderId"", ""type"": ""string"" }
                ],
                ""name"": ""getAnchor"",
                ""outputs"": [
                    { ""internalType"": ""bytes32"", ""name"": """", ""type"": ""bytes32"" }
                ],
                ""stateMutability"": ""view"",
                ""type"": ""function""
            }
        ]";
    }
}
