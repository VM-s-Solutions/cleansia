using System.Text.Json;
using Cleansia.Core.Clients.Abstractions.Apns;
using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Consumes <see cref="SendLiveActivityUpdateMessage"/> from <c>live-activity-dispatch</c> (ADR-0029
/// D2), resolves the order's <see cref="LiveActivityToken"/> rows, builds the ActivityKit payload, and
/// sends per token via <see cref="ILiveActivityPushClient"/>. A SIBLING of
/// <c>SendPushNotificationHandler</c> — separate queue, separate claim keyspace, separate failure
/// domain — never a modification of it (an FCM outage cannot retry-storm APNs, and vice versa).
///
/// <para>ADR-0002 D2.2 — GUARD-FIRST (claim-then-act). ActivityKit sends are non-transactional and
/// have no domain target-state, so the deterministic key is claimed BEFORE the send; a redelivery
/// short-circuits. AT-MOST-ONCE AFTER THE MARKER — a crash between claim and send loses that one
/// update, healed by the next transition (a lost terminal is bounded by the widget's stale-date, the OS
/// 8h cap, and the 24h janitor). Envelope-ONLY read: the queue is net-new (no pre-envelope traffic) and
/// the claim key carries the transition <c>Sequence</c> which the bare payload does not — so there is no
/// D2.1a dual-read to synthesize (recorded deliberately, ADR-0029 / T-0427 LA-4).</para>
///
/// <para>INERT ship: with <c>APNS:Enabled=false</c> (or no token) the client reports Skipped and this
/// consumer acks, sending nothing — exactly how the whole channel ships behind the config flag.</para>
/// </summary>
public class SendLiveActivityUpdateHandler(
    ILiveActivityTokenRepository liveActivityTokenRepository,
    IOrderRepository orderRepository,
    ILiveActivityPushClient liveActivityPushClient,
    IUnitOfWork unitOfWork,
    IIdempotencyGuard idempotencyGuard,
    ITenantProvider tenantProvider,
    ILogger<SendLiveActivityUpdateHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        // ── ADR-0002 D3.3 — PERMANENT failures (malformed body / missing fields) log at Warning + ACK ──
        // S6: never log the raw body — it is lock-screen state; log only its size.
        QueueEnvelope<SendLiveActivityUpdateMessage>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<QueueEnvelope<SendLiveActivityUpdateMessage>>(messageText, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Discarding live-activity message: malformed/un-deserializable body (permanent), {Bytes} bytes",
                messageText.Length);
            return;
        }

        var message = envelope?.Payload;
        if (message is null)
        {
            logger.LogWarning("Discarding live-activity message: deserialized to null (permanent), {Bytes} bytes", messageText.Length);
            return;
        }

        if (string.IsNullOrEmpty(message.UserId) || string.IsNullOrEmpty(message.OrderId)
            || string.IsNullOrEmpty(message.EventKey) || string.IsNullOrEmpty(envelope!.MessageKey))
        {
            logger.LogWarning(
                "Discarding live-activity message with missing UserId/OrderId/EventKey/MessageKey ({Bytes} bytes)",
                messageText.Length);
            return;
        }

        // ── ADR-0002 D2.2 — CLAIM-FIRST, before any tenant-scoped read or the APNs send. The envelope's
        //    MessageKey is authoritative (it carries the transition Sequence the bare payload cannot). ──
        if (await idempotencyGuard.AlreadyProcessedAsync(envelope.MessageKey, ct))
        {
            logger.LogInformation("Live-activity {MessageKey} already dispatched, skipping (idempotent)", envelope.MessageKey);
            return;
        }

        try
        {
            // Cross-tenant lookup — the queue trigger has no JWT, so the EF global filter has no tenant.
            var tenantId = !string.IsNullOrEmpty(envelope.TenantId) ? envelope.TenantId : message.TenantId;
            if (!string.IsNullOrEmpty(tenantId))
            {
                tenantProvider.SetTenantOverride(tenantId);
            }

            var isStart = message.EventKey == LiveActivityEventKeys.Start;
            var isEnd = message.EventKey == LiveActivityEventKeys.End;

            // Start targets the per-install push-to-start tokens (OrderId == null); update/end target the
            // order's update tokens (OrderId == orderId).
            var tokens = isStart
                ? await liveActivityTokenRepository.GetPushToStartTokensAsync(message.UserId, ct)
                : await liveActivityTokenRepository.GetByUserAndOrderAsync(message.UserId, message.OrderId, ct);

            if (tokens.Count == 0)
            {
                logger.LogInformation(
                    "No live-activity tokens for user {UserId} order {OrderId} on event {EventKey} — acking",
                    message.UserId, message.OrderId, message.EventKey);
                return;
            }

            // Only an END event needs the order's terminal status (Completed vs Cancelled) — they share
            // EventKey=end but differ in content-state status + dismissal window.
            OrderStatusForEnd? currentStatus = isEnd
                ? new OrderStatusForEnd((await orderRepository.GetByIdAsync(message.OrderId, ct))?.CurrentStatus)
                : null;

            var push = LiveActivityPayloadFactory.Build(message, currentStatus?.Value, DateTimeOffset.UtcNow);

            var delivered = 0;
            var pruned = 0;
            var skipped = false;
            foreach (var token in tokens)
            {
                var result = await liveActivityPushClient.SendAsync(token.Token, push, ct);

                if (result.Skipped)
                {
                    // Provider disabled/keyless — applies to every token. Ack, send nothing more.
                    skipped = true;
                    break;
                }

                if (result.TokenInvalid)
                {
                    // For a terminal event every order row is deleted below anyway; for start/update prune
                    // the dead row so it is not re-attempted next transition.
                    if (!isEnd)
                    {
                        liveActivityTokenRepository.Remove(token);
                        pruned++;
                    }
                }
                else if (result.Delivered)
                {
                    delivered++;
                }
            }

            if (skipped)
            {
                logger.LogInformation(
                    "Live-activity dispatch skipped (APNS disabled/unconfigured) for order {OrderId} event {EventKey} — acking",
                    message.OrderId, message.EventKey);
                return;
            }

            // Terminal cleanup — a successful end send retires the activity, so hard-delete the order's
            // rows (cleanup path 1). A transient send THREW above, so we never reach here on failure; the
            // janitor is the backstop for that case.
            if (isEnd)
            {
                liveActivityTokenRepository.RemoveRange(tokens);
                await unitOfWork.CommitAsync(ct);
            }
            else if (pruned > 0)
            {
                await unitOfWork.CommitAsync(ct);
            }

            logger.LogInformation(
                "Live-activity dispatch for {EventKey} on order {OrderId}: {Delivered} delivered, {Pruned} pruned, {Total} tokens",
                message.EventKey, message.OrderId, delivered, pruned, tokens.Count);
        }
        catch (Exception ex)
        {
            // ── ADR-0002 D3.3 — TRANSIENT/INFRA fault (a DB read fault, the APNs network call, or the
            //    terminal-cleanup commit). We re-throw, but the claim above already committed in its own
            //    unit of work, so the queue REDELIVERY SHORT-CIRCUITS on the idempotency marker and acks:
            //    this is AT-MOST-ONCE-AFTER-THE-MARKER — exactly like SendPushNotificationHandler — NOT a
            //    retry-to-poison of the send. A blip after the claim DROPS that one activity update, which
            //    is acceptable here: a dropped UPDATE self-heals on the next order-status transition, and a
            //    dropped END is bounded by the content-state stale-date, the OS ~8h force-end, and the 24h
            //    janitor (D3). (A PRE-claim fault — including the claim's own commit — is uncaught here and
            //    genuinely redelivers, since the marker never landed.)
            //    S6: log only the safe scalar correlation keys, never the raw body. ──
            logger.LogError(ex,
                "Transient/infra failure dispatching live-activity update — will retry via queue for order {OrderId} on event {EventKey}",
                message.OrderId, message.EventKey);
            throw;
        }
    }

    // A tiny wrapper so a fetched-but-status-null order (Value == null) is distinguishable from "not an
    // end event" (the whole struct is null) — the end payload then falls back to the completed branch.
    private readonly record struct OrderStatusForEnd(Cleansia.Core.Domain.Enums.OrderStatus? Value);
}
