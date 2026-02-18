using LedgeLink.Distributor.API.Application.DTOs;
using LedgeLink.Distributor.API.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace LedgeLink.Distributor.API.API.Controllers;

/// <summary>
/// API layer: HTTP entry point — nothing more.
///
/// Responsibilities:
///   - Parse and validate the HTTP request
///   - Delegate entirely to SubmitTradeUseCase
///   - Map the use case result to the appropriate HTTP response code
///
/// This controller has ZERO business logic. Zero.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class TradesController : ControllerBase
{
    private readonly SubmitTradeUseCase _submitTrade;
    private readonly ILogger<TradesController> _logger;

    public TradesController(SubmitTradeUseCase submitTrade, ILogger<TradesController> logger)
    {
        _submitTrade = submitTrade;
        _logger      = logger;
    }

    /// <summary>
    /// Submit a trade instruction.
    /// Returns 201 Created for new trades, 200 OK for idempotent duplicates.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SubmitTradeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(SubmitTradeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] SubmitTradeRequest request, CancellationToken ct)
    {
        // ── Input guard (pure HTTP concern — not a domain rule) ──────────────
        if (string.IsNullOrWhiteSpace(request.ExternalOrderId))
            return BadRequest(new { error = "ExternalOrderId is required." });

        if (request.Amount <= 0)
            return BadRequest(new { error = "Amount must be greater than zero." });

        // ── Delegate entirely to the use case ────────────────────────────────
        var result = await _submitTrade.ExecuteAsync(request, ct);

        var response = new SubmitTradeResponse
        {
            TradeId         = result.Trade.InternalId,
            ExternalOrderId = result.Trade.ExternalOrderId,
            Status          = result.Trade.Status.ToString(),
            Timestamp       = result.Trade.Timestamp,
            Message         = result.IsNew
                                ? "Trade accepted and queued for settlement."
                                : "Duplicate — returning original trade record.",
            IsDuplicate     = result.IsDuplicate
        };

        return result.IsNew
            ? CreatedAtAction(nameof(GetById), new { id = result.Trade.InternalId }, response)
            : Ok(response);
    }

    /// <summary>Get a single trade by its internal ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        // Lightweight — direct repo read for GET is fine (no use case needed)
        var repo  = HttpContext.RequestServices.GetRequiredService<Application.Interfaces.ITradeRepository>();
        var trade = await repo.FindByIdAsync(id, ct);
        return trade is null ? NotFound() : Ok(trade);
    }

    /// <summary>List the 50 most recent trades.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var repo   = HttpContext.RequestServices.GetRequiredService<Application.Interfaces.ITradeRepository>();
        var trades = await repo.GetRecentAsync(50, ct);
        return Ok(trades);
    }
}
