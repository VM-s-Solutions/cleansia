using System.Reflection;
using Cleansia.Config;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        // Committed production cron defaults for the four recurring/notification timers
        // (the %AppSetting% TimerTrigger tokens resolve from these). The Functions platform
        // app-settings (env) and, in dev, local.settings.json Values override them, so
        // promotion is config-only.
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        // HostBuilder doesn't auto-load user secrets like WebApplication does.
        if (context.HostingEnvironment.IsDevelopment())
        {
            config.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
        }
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddHttpContextAccessor();

        services.AddCoreBindings(context.Configuration, context.HostingEnvironment);

        // Sentinel binding — MediatR's assembly scan registers the Auth handlers
        // which depend on IHostAudienceProvider; the Functions host never issues
        // tokens but DI still validates the ctor at startup.
        services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider("cleansia.functions"));

        services.AddScoped<IPayPeriodBackgroundService, PayPeriodBackgroundService>();
        services.AddScoped<IPeriodReminderBackgroundService, PeriodReminderBackgroundService>();
        services.AddScoped<INewJobsDigestService, NewJobsDigestService>();
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
        services.AddScoped<IRefreshTokenCleanupService, RefreshTokenCleanupService>();

        // ADR-0002 D5 step 1 — the testable consumer bodies live in
        // Cleansia.Functions.Core. The [Function] trigger shells (Functions/*.cs) stay in this
        // Exe so the Worker SDK source-gen still discovers all 16 triggers, and resolve their
        // Core handler via DI. Scoped because the handlers pull scoped repos / IUnitOfWork.
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
        // ADR-0002 D3.4 — the dispatch reconciliation timer body (sibling to
        // RetryFailedFiscalRegistrations; the [TimerTrigger] shell is FiscalReconciliationFunction).
        services.AddScoped<FiscalReconciliationTimerHandler>();
        // The single dedicated outbox drainer body. This host is the ONE place the outbox is drained
        // (the [TimerTrigger] singleton shell is OutboxDrainerFunction); the host still keeps the
        // post-commit dispatch behavior so an in-Function command writes a durable row.
        services.AddScoped<OutboxDrainerTimerHandler>();
        services.AddScoped<AutoCancelStaleRecurringOrdersHandler>();
        services.AddScoped<CleanupStalePendingOrdersHandler>();
        services.AddScoped<MaterializeRecurringBookingsHandler>();
        services.AddScoped<RefreshTokenCleanupTimerHandler>();
        services.AddScoped<SendMembershipLifecycleNotificationsHandler>();
        services.AddScoped<SendRecurringOrderRemindersHandler>();
        services.AddScoped<SendNewJobsDigestTimerHandler>();
        services.AddScoped<ExpireStaleReferralsHandler>();

        // ADR-0002 D3 (F3) — the per-queue -poison consumers. Each [QueueTrigger]
        // "<queue>-poison" shell (Functions/*PoisonFunction.cs) resolves its Core handler here; the
        // handler records a durable DeadLetter row (IDeadLetterStore) + LogError (alert) + acks. The
        // store itself (IDeadLetterStore) is registered in AddCoreBindings → AddRepositories.
        services.AddScoped<GenerateReceiptPoisonHandler>();
        services.AddScoped<GenerateInvoicePoisonHandler>();
        services.AddScoped<NotificationsDispatchPoisonHandler>();
        services.AddScoped<SitewidePromoFanoutPoisonHandler>();
        services.AddScoped<CalculateOrderPayPoisonHandler>();
        services.AddScoped<SendEmailPoisonHandler>();
    })
    .Build();

host.Run();
