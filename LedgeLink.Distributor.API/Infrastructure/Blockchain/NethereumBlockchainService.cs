using LedgeLink.Distributor.API.Application.Interfaces;
using Nethereum.Web3;
using Nethereum.Hex.HexConvertors.Extensions;

namespace LedgeLink.Distributor.API.Infrastructure.Blockchain;

public sealed class NethereumBlockchainService : IBlockchainService
{
    private readonly string? _rpcUrl;
    private readonly string? _contractAddress;
    private readonly ILogger<NethereumBlockchainService> _logger;

    public NethereumBlockchainService(IConfiguration configuration, ILogger<NethereumBlockchainService> logger)
    {
        _rpcUrl = configuration["Ethereum:RpcUrl"];
        _contractAddress = configuration["Ethereum:ContractAddress"];
        _logger = logger;
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
                    { ""internalType"": ""string"", ""name"": ""externalOrderId"", ""type"": ""string"" }
                ],
                ""name"": ""getAnchor"",
                ""outputs"": [
                    { ""internalType"": ""bytes32"", ""name"": """", ""type"": ""bytes32"" }
                ],
                ""stateMutability"": ""view"", ""type"": ""function""
            }
        ]";
    }
}
