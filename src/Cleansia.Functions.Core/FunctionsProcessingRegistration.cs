using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Functions.Core;

/// <summary>
/// The SINGLE source of truth for the Functions host's background-service + per-trigger handler
/// registrations. <c>Program.cs</c> and <c>FunctionsHostStartupGuardTests</c> both call this, so the
/// guard test resolves against the REAL registration set — a handler added to
/// <c>Cleansia.Functions.Core.Handlers</c> but never registered here fails the guard test (its reflection
/// sweep finds the type but the container can't resolve it) instead of surfacing as an opaque
/// "Application Error" 503 at trigger time in production (the 2026-07-18 silent-outage class).
///
/// <para>Deliberately does NOT register <c>FunctionsHealthCheck</c> (a <c>Cleansia.Config</c> type this
/// assembly does not reference) — its one-line registration stays with each caller.</para>
/// </summary>
public static class FunctionsProcessingRegistration
{
    public static IServiceCollection AddFunctionsProcessing(this IServiceCollection services)
    {
        services.AddScoped<IPayPeriodBackgroundService, PayPeriodBackgroundService>();
        services.AddScoped<IPeriodReminderBackgroundService, PeriodReminderBackgroundService>();
        services.AddScoped<INewJobsDigestService, NewJobsDigestService>();
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
        services.AddScoped<IRefreshTokenCleanupService, RefreshTokenCleanupService>();

        // ADR-0002 D5 step 1 — the testable consumer bodies. The [Function] trigger shells
        // (Cleansia.Functions/Functions/*.cs) stay in the Exe so the Worker SDK source-gen discovers all
        // triggers, and resolve their Core handler via DI. Scoped: the handlers pull scoped repos / IUnitOfWork.
        services.AddScoped<GenerateReceiptHandler>();
        services.AddScoped<GenerateInvoiceHandler>();
        services.AddScoped<SendPushNotificationHandler>();
        services.AddScoped<SendEmailHandler>();
        services.AddScoped<CalculateOrderPayHandler>();
        services.AddScoped<SendSitewidePromoFanoutHandler>();
        services.AddScoped<PayPeriodTimerHandler>();
        services.AddScoped<DataRetentionTimerHandler>();
        services.AddScoped<PeriodReminderTimerHandler>();
        services.AddScoped<RetryFailedFiscalRegistrationsHandler>();
        // ADR-0002 D3.4 — dispatch reconciliation timer body (shell: FiscalReconciliationFunction).
        services.AddScoped<FiscalReconciliationTimerHandler>();
        // The single dedicated outbox drainer body (singleton shell: OutboxDrainerFunction).
        services.AddScoped<OutboxDrainerTimerHandler>();
        // Outbox retention prune body (shell: PruneOutboxFunction) — table-growth hygiene.
        services.AddScoped<PruneOutboxTimerHandler>();
        services.AddScoped<AutoCancelStaleRecurringOrdersHandler>();
        services.AddScoped<CleanupStalePendingOrdersHandler>();
        services.AddScoped<MaterializeRecurringBookingsHandler>();
        services.AddScoped<RefreshTokenCleanupTimerHandler>();
        services.AddScoped<SendMembershipLifecycleNotificationsHandler>();
        services.AddScoped<SendRecurringOrderRemindersHandler>();
        services.AddScoped<SendNewJobsDigestTimerHandler>();
        services.AddScoped<ExpireStaleReferralsHandler>();

        // ADR-0002 D3 (F3) — the per-queue -poison consumers. Each records a durable DeadLetter row +
        // LogError + acks; IDeadLetterStore is registered in AddCoreBindings → AddRepositories.
        services.AddScoped<GenerateReceiptPoisonHandler>();
        services.AddScoped<GenerateInvoicePoisonHandler>();
        services.AddScoped<NotificationsDispatchPoisonHandler>();
        services.AddScoped<SitewidePromoFanoutPoisonHandler>();
        services.AddScoped<CalculateOrderPayPoisonHandler>();
        services.AddScoped<SendEmailPoisonHandler>();

        return services;
    }
}
