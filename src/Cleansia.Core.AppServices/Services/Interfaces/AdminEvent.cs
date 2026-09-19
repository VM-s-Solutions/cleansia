namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <param name="Key">An <c>AdminNotificationEventCatalog</c> key.</param>
/// <param name="TenantId">The company whose administrators are told.</param>
/// <param name="Subject">
/// The resource id the event is about, plus a discriminator where one resource can raise the event
/// more than once. Unique per logical event across requests — the e-mail channel will key its outbox
/// row on it and fail a repeat at commit rather than collapse it, so a site that can raise one event
/// twice for one resource guards before calling.
/// </param>
/// <param name="Args">
/// Loc-args for the feed row and the e-mail body: exactly the names the event's catalogue entry
/// declares, no more and no fewer. Never a person: no name, contact, address or free text. Always the
/// id(s) the console deep-links from.
/// </param>
public sealed record AdminEvent(
    string Key,
    string TenantId,
    string Subject,
    IReadOnlyDictionary<string, string> Args);
