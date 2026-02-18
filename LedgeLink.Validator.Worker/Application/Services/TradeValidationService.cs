using LedgeLink.Shared.Application.Interfaces;
using LedgeLink.Shared.Domain.Enums;
using LedgeLink.Shared.Domain.Models;
using LedgeLink.Validator.Worker.Application.Interfaces;
using LedgeLink.Validator.Worker.Domain.Rules;

namespace LedgeLink.Validator.Worker.Application.Services;

/// <summary>
/// Application service: runs all domain ValidationRules against a TradeToken,
/// then publishes the correct outcome event (validated or rejected).
///
/// Knows about: domain rules, IMessagePublisher (via interface), QueueNames.
/// Does NOT know about: RabbitMQ.Client, channels, bytes, JSON.
/// </summary>
public sealed class TradeValidationService
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<TradeValidationService> _logger;

    // All rules registered here — easy to add/remove without touching worker logic
    private static readonly ValidationRule[] Rules =
    [
        new ExternalOrderIdRequiredRule(),
        new AmountPositiveRule(),
        new AmountMaximumRule(),
        new DistributorRequiredRule(),
        new AssetManagerRequiredRule(),
        new TimestampNotFutureRule()
    ];

    public TradeValidationService(IMessagePublisher publisher, ILogger<TradeValidationService> logger)
    {
        _publisher = publisher;
        _logger    = logger;
    }

    public async Task ValidateAndPublishAsync(TradeToken trade, CancellationToken ct)
    {
        _logger.LogInformation(
            "Validating {ExternalOrderId} | Amount: £{Amount:N2}",
            trade.ExternalOrderId, trade.Amount);

        // Run every rule — fail-fast on first violation
        foreach (var rule in Rules)
        {
            var rejection = rule.Validate(trade);
            if (rejection is not null)
            {
                trade.Status          = TradeStatus.Rejected;
                trade.RejectionReason = rejection;

                _logger.LogWarning(
                    "Trade {ExternalOrderId} REJECTED by rule [{Rule}]: {Reason}",
                    trade.ExternalOrderId, rule.RuleName, rejection);

                await _publisher.PublishAsync(trade, QueueNames.TradeRejected, ct);
                return;
            }
        }

        trade.Status = TradeStatus.Validated;
        _logger.LogInformation(
            "Trade {ExternalOrderId} VALIDATED — all {Count} rules passed.",
            trade.ExternalOrderId, Rules.Length);

        await _publisher.PublishAsync(trade, QueueNames.TradeValidated, ct);
    }
}
