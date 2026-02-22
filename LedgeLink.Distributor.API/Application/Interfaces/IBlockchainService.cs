namespace LedgeLink.Distributor.API.Application.Interfaces;

public interface IBlockchainService
{
    Task<(bool IsAnchored, string? AnchoredHash)> GetAnchoredHashAsync(string externalOrderId, CancellationToken ct = default);
}
