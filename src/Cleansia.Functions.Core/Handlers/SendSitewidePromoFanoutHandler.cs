using System.Text.Json;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Fan-out consumer for the admin "send sitewide promo" action.
///
/// One <see cref="SendSitewidePromoMessage"/> is enqueued by
/// <c>AdminMarketingController.SendSitewidePromo</c> per campaign. This
/// handler pages through <see cref="UserNotificationPreferences"/> rows
/// where the user has opted in to the <c>Promo</c> category, joins the
/// matching <see cref="Domain.Users.User"/> for locale, and enqueues a
/// <see cref="SendPushNotificationMessage"/> per recipient on
/// <c>notifications-dispatch</c> carrying the locale-matched title+body.
///
/// Why fan-out happens HERE rather than in the synchronous request:
///   - The admin's POST returns immediately even on million-user sends.
///   - Azure Storage Queues throttle write throughput by partition. Paging
///     and streaming with small delays keeps us well under the rate limit.
///   - Retries (e.g. transient queue failure mid-fan-out) are handled by
///     the queue's poison-message pipeline without re-prompting the admin.
///
/// Locale resolution: looks at <c>User.PreferredLanguageCode</c>; falls back
/// to "en" when unset or when the language isn't one of the 5 we support.
/// </summary>
public class SendSitewidePromoFanoutHandler(
    IUserNotificationPreferencesRepository preferencesRepository,
    IUserRepository userRepository,
    IQueueClient queueClient,
    ICampaignProgressStore campaignProgressStore,
    ITenantProvider tenantProvider,
    ILogger<SendSitewidePromoFanoutHandler> logger)
{
    /// <summary>
    /// Page size for the opted-in user query. Bigger pages = fewer DB
    /// round-trips but more memory + queue burst per batch. 200 is a
    /// conservative middle ground; the per-user push messages each fan out
    /// to their own consumer downstream. Overridable so a test can force
    /// multiple pages (and a between-page failure) without seeding 200+ rows.
    /// </summary>
    private readonly int _pageSize = DefaultPageSize;

    private const int DefaultPageSize = 200;

    public SendSitewidePromoFanoutHandler(
        IUserNotificationPreferencesRepository preferencesRepository,
        IUserRepository userRepository,
        IQueueClient queueClient,
        ICampaignProgressStore campaignProgressStore,
        ITenantProvider tenantProvider,
        ILogger<SendSitewidePromoFanoutHandler> logger,
        int pageSize)
        : this(preferencesRepository, userRepository, queueClient, campaignProgressStore, tenantProvider, logger)
    {
        _pageSize = pageSize;
    }

    /// <summary>
    /// Locales we ship with. Anything else falls back to "en". Kept in sync
    /// with the mobile customer app's <c>values-*</c> resource dirs.
    /// </summary>
    private static readonly HashSet<string> SupportedLocales =
        new(StringComparer.OrdinalIgnoreCase) { "en", "cs", "sk", "uk", "ru" };

    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        SendSitewidePromoMessage? campaign = null;
        try
        {
            campaign = JsonSerializer.Deserialize<SendSitewidePromoMessage>(
                messageText,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize SendSitewidePromoMessage: {messageText}");

            // No JWT on the queue trigger — set the tenant override from the
            // campaign so the global filter scopes to the right partition.
            if (!string.IsNullOrEmpty(campaign.TenantId))
            {
                tenantProvider.SetTenantOverride(campaign.TenantId);
            }

            // Defensive: the validator on the synchronous handler already
            // enforces non-empty bodies. A campaign that somehow lands here
            // with missing locales would still serve users via the en fallback.
            if (!campaign.TitleByLocale.ContainsKey("en") || !campaign.BodyByLocale.ContainsKey("en"))
            {
                logger.LogWarning(
                    "Discarding sitewide promo campaign without en title/body fallback: {Message}",
                    messageText);
                return;
            }

            // Page through opted-in users. We join on PreferredLanguageCode so
            // the per-user enqueue carries the localized strings inline — the
            // downstream consumer doesn't need to look up locale again.
            //
            // S8: SCOPE THE QUERY TO THE CAMPAIGN'S TENANT. The previous code used
            // GetQueryableIgnoringTenant() on both sides with NO tenant predicate, so SetTenantOverride
            // had zero effect once filters were ignored — one tenant's campaign fanned out to opted-in
            // users of EVERY tenant. We keep IgnoreQueryFilters (the override is not load-bearing) and
            // add an EXPLICIT TenantId predicate. A null campaign.TenantId means single-tenant mode and
            // matches the (null) rows for that deployment.
            var campaignTenantId = campaign.TenantId;
            var query = preferencesRepository.GetQueryableIgnoringTenant()
                .Where(p => p.Promo)
                .Join(userRepository.GetQueryableIgnoringTenant(),
                    p => p.UserId,
                    u => u.Id,
                    (p, u) => new { p.UserId, u.PreferredLanguageCode, p.TenantId })
                .Where(x => x.TenantId == campaignTenantId)
                // Stable order so paged reads don't skip rows.
                .OrderBy(x => x.UserId);

            // ADR-0002 D2.3 — per-campaign RESUME cursor (the named nice-to-have, a cost/spam layer on
            // top of the downstream push:{UserId}:{EventKey} effect dedup, which this never replaces).
            // On redelivery, resume from the last persisted cursor (the last fully-processed page’s
            // last UserId, consistent with the OrderBy(UserId) stable order) instead of restarting at
            // offset 0, so a flaky page read does not re-enqueue (re-cost) the recipients already
            // processed. A campaign whose cursor reached the end is recognized complete and is a no-op.
            var progress = await campaignProgressStore.GetAsync(campaign.CampaignId, ct);
            if (progress.IsComplete)
            {
                logger.LogInformation(
                    "Sitewide promo campaign {CampaignId} already complete — redelivery is a no-op",
                    campaign.CampaignId);
                return;
            }

            // KEYSET (seek) paging instead of Skip(offset). UserId is a unique key, so
            // `WHERE UserId > lastUserId` avoids Postgres scanning+discarding `offset` rows per page —
            // the old Skip(offset) was O(N^2) over the doc-stated million-user fan-out.
            var totalEnqueued = 0;
            var lastUserId = progress.LastProcessedUserId;
            while (true)
            {
                var pageQuery = query.AsQueryable();
                if (lastUserId is not null)
                {
                    pageQuery = pageQuery.Where(x => string.Compare(x.UserId, lastUserId) > 0);
                }

                var page = await pageQuery
                    .Take(_pageSize)
                    .ToListAsync(ct);

                if (page.Count == 0) break;
                lastUserId = page[^1].UserId;

                foreach (var recipient in page)
                {
                    var locale = ResolveLocale(recipient.PreferredLanguageCode);
                    var title = campaign.TitleByLocale.TryGetValue(locale, out var t) ? t : campaign.TitleByLocale["en"];
                    var body = campaign.BodyByLocale.TryGetValue(locale, out var b) ? b : campaign.BodyByLocale["en"];

                    try
                    {
                        await queueClient.SendAsync(
                            QueueNames.NotificationsDispatch,
                            new SendPushNotificationMessage(
                                UserId: recipient.UserId,
                                EventKey: NotificationEventCatalog.PromoNewSitewide,
                                Args: new Dictionary<string, string>
                                {
                                    ["title"] = title,
                                    ["body"] = body,
                                },
                                TenantId: recipient.TenantId),
                            ct);
                        totalEnqueued++;
                    }
                    catch (Exception ex)
                    {
                        // One user's enqueue failed — log and continue. The
                        // campaign at large should not poison the queue and
                        // retry the entire fan-out from scratch over a
                        // transient hiccup on a single message.
                        logger.LogWarning(ex,
                            "Failed to enqueue sitewide promo for user {UserId}; continuing",
                            recipient.UserId);
                    }
                }

                // The page finished — persist the cursor so a crash/redelivery after this point
                // resumes PAST it rather than re-paging from offset 0. F8’s named failure (a later
                // page read throwing) lands in the outer catch and redelivers; the cursor already
                // reflects every fully-processed page, so the retry seeks past them.
                await campaignProgressStore.AdvanceAsync(campaign.CampaignId, lastUserId, ct);

                if (page.Count < _pageSize) break;
            }

            await campaignProgressStore.MarkCompleteAsync(campaign.CampaignId, ct);

            logger.LogInformation(
                "Sitewide promo fan-out complete: enqueued {Count} user-level push messages",
                totalEnqueued);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed sitewide promo fan-out. Message: {Message}",
                messageText);
            throw; // poison-message pipeline retries the whole campaign.
        }
    }

    private static string ResolveLocale(string? preferredLanguageCode)
    {
        if (string.IsNullOrWhiteSpace(preferredLanguageCode)) return "en";
        return SupportedLocales.Contains(preferredLanguageCode)
            ? preferredLanguageCode.ToLowerInvariant()
            : "en";
    }
}
