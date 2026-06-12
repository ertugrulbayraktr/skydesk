using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;

namespace Support.Infrastructure.BackgroundServices;

/// <summary>
/// Polls the classification outbox and applies AI-suggested category/priority
/// to tickets. DB-backed (outbox pattern): pending work survives restarts,
/// failures are retried up to MaxAttempts with the error recorded on the row.
/// </summary>
public class ClassificationWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AiTimeout = TimeSpan.FromSeconds(30);
    private const int MaxAttempts = 3;
    private const int BatchSize = 10;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ClassificationWorker> _logger;

    public ClassificationWorker(IServiceProvider serviceProvider, ILogger<ClassificationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Classification worker started (outbox polling, every {Interval}s)", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Classification worker poll failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Classification worker stopped");
    }

    internal async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var pending = await context.ClassificationOutbox
            .Where(o => o.Status == OutboxStatus.Pending)
            .OrderBy(o => o.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0) return;

        var reservationProvider = scope.ServiceProvider.GetRequiredService<IReservationProvider>();
        var aiCopilot = scope.ServiceProvider.GetRequiredService<IAiCopilotClient>();

        foreach (var item in pending)
        {
            var now = DateTime.UtcNow;
            try
            {
                var ticket = await context.Tickets
                    .FirstOrDefaultAsync(t => t.Id == item.TicketId, cancellationToken);

                if (ticket == null || ticket.State != TicketState.New)
                {
                    // Ticket gone or already triaged by an agent — AI result no longer relevant
                    item.MarkCompleted(now);
                    continue;
                }

                var reservation = item.PNR != null && item.PassengerLastName != null
                    ? await reservationProvider.GetReservationAsync(item.PNR, item.PassengerLastName, cancellationToken)
                    : null;

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(AiTimeout);
                var draft = await aiCopilot.DraftTicketCreateAsync(item.FreeText, reservation, timeoutCts.Token);

                if (ticket.ApplyClassification(draft.CategorySuggested, draft.PrioritySuggested))
                {
                    context.TicketAuditEvents.Add(new TicketAuditEvent(
                        ticket.Id,
                        ActorType.System,
                        AuditEventType.PriorityChanged,
                        details: $"AI classification applied. Category: {draft.CategorySuggested}, Priority: {draft.PrioritySuggested}. Summary: {draft.Summary}"));

                    _logger.LogInformation("Ticket {TicketId} classified as {Category}/{Priority}",
                        ticket.Id, draft.CategorySuggested, draft.PrioritySuggested);
                }

                item.MarkCompleted(now);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                item.RegisterFailure(ex.Message, MaxAttempts, now);
                _logger.LogWarning(ex,
                    "Classification attempt {Attempt}/{Max} failed for ticket {TicketId}",
                    item.Attempts, MaxAttempts, item.TicketId);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
