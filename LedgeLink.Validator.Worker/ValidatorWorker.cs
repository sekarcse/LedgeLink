using System.Text.Json;
using Azure.Messaging.ServiceBus;
using LedgeLink.Shared.Application.Interfaces;
using LedgeLink.Shared.Domain.Models;
using LedgeLink.Validator.Worker.Application.Services;
using LedgeLink.Validator.Worker.Infrastructure.Messaging;

namespace LedgeLink.Validator.Worker;

/// <summary>
/// Hosted service entry point — infrastructure only.
///
/// Responsibilities (thin):
///   - Set up the Service Bus processor
///   - Deserialise the message envelope
///   - Hand the domain entity to TradeValidationService
///   - Complete or Abandon based on the outcome
///
/// Zero business logic here. All rules live in Domain/Rules and Application/Services.
/// </summary>
public sealed class ValidatorWorker : BackgroundService
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly TradeValidationService _validationService;
    private readonly ServiceBusMessagePublisher _publisher;
    private readonly ILogger<ValidatorWorker> _logger;
    private ServiceBusProcessor? _processor;

    public ValidatorWorker(
        ServiceBusClient serviceBusClient,
        TradeValidationService validationService,
        ServiceBusMessagePublisher publisher,
        ILogger<ValidatorWorker> logger)
    {
        _serviceBusClient   = serviceBusClient;
        _validationService  = validationService;
        _publisher          = publisher;
        _logger             = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Validator.Worker starting...");

        await _publisher.EnsureTopologyAsync(stoppingToken);

        _processor = _serviceBusClient.CreateProcessor(QueueNames.TradeRequested, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation("Validator.Worker listening on '{Queue}'", QueueNames.TradeRequested);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        TradeToken? trade = null;
        try
        {
            var json = args.Message.Body.ToString();
            trade = JsonSerializer.Deserialize<TradeToken>(json);

            if (trade is null)
            {
                _logger.LogError("Failed to deserialise message. Discarding.");
                await args.DeadLetterMessageAsync(args.Message, cancellationToken: args.CancellationToken);
                return;
            }

            // Hand off to the application service — no domain logic here
            await _validationService.ValidateAndPublishAsync(trade, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing {ExternalOrderId}", trade?.ExternalOrderId ?? "?");
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Service Bus error: {ErrorSource}", args.ErrorSource);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(ct);
            await _processor.DisposeAsync();
        }
        await base.StopAsync(ct);
    }

    public override void Dispose()
    {
        _processor?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
