using System.Globalization;
using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Infra.Services.Pdf.IncidentFile;

public static class IncidentFileSections
{
    public const string IdentityTitle = "1. Identity as of export";
    public const string OrdersTitle = "2. Orders";
    public const string DisputesTitle = "3. Disputes";
    public const string ConsentsTitle = "4. Consents";
    public const string TrailTitle = "5. Action trail";

    public const string ErasedMarker = "erased";
    public const string Empty = "—";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static IReadOnlyList<IncidentFileSection> Build(IncidentFilePdfData data) =>
    [
        Identity(data.Subject),
        Orders(data),
        Disputes(data.Disputes),
        Consents(data.Consents),
        Trail(data),
    ];

    private static IncidentFileSection Identity(IncidentFileSubject subject)
    {
        // An erased subject's name, e-mail and phone are the anonymisation marker or null in the
        // database; the file says "erased" instead of printing the marker as if it were a name.
        string Identity(string? value) => subject.Erased ? ErasedMarker : Text(value);

        var fields = new List<IncidentFileField>
        {
            new("User id", subject.UserId),
            new("Name", subject.Erased ? ErasedMarker : $"{subject.FirstName} {subject.LastName}".Trim()),
            new("E-mail", Identity(subject.Email)),
            new("Phone", Identity(subject.PhoneNumber)),
            new("Account created", Stamp(subject.AccountCreatedOn)),
            new("Operator", Text(subject.Operator)),
            new("Preferred language", Text(subject.PreferredLanguage)),
            new("Erased", subject.Erased ? $"yes, {Stamp(subject.ErasedOn)}" : "no"),
        };

        return new IncidentFileSection(IdentityTitle, [new IncidentFileFieldList(fields)]);
    }

    private static IncidentFileSection Orders(IncidentFilePdfData data)
    {
        var blocks = new List<IncidentFileBlock>();
        if (data.OrderIdFilter is not null)
        {
            blocks.Add(new IncidentFileParagraph($"Scoped to order {data.OrderIdFilter}."));
        }

        if (data.Orders.Count == 0)
        {
            blocks.Add(new IncidentFileParagraph("No orders."));
        }

        foreach (var order in data.Orders)
        {
            blocks.Add(new IncidentFileSubheading($"Order {order.Number}"));
            blocks.Add(new IncidentFileFieldList(
            [
                new("Order id", order.Id),
                new("Booked", Stamp(order.CreatedOn)),
                new("Cleaning", Stamp(order.CleaningDateTime)),
                new("Completed", Stamp(order.CompletedAt)),
                new("Cancelled", Stamp(order.CancelledAt)),
                new("Address", order.Address),
                new("Total", Money(order.TotalPrice, order.Currency)),
                new("Payment", $"{order.PaymentType} / {order.PaymentStatus}"),
                new("Status", order.Status),
                new("Cancelled by", Text(order.CancelledBy)),
                new("Cancellation reason", Text(order.CancellationReason)),
                new("Assigned cleaners", order.AssignedCleaners.Count == 0
                    ? Empty
                    : string.Join("; ", order.AssignedCleaners.Select(c => $"{c.FirstName} ({c.EmployeeId})"))),
            ]));

            blocks.Add(new IncidentFileTable(
                "Lines",
                ["Kind", "Name", "Amount"],
                order.Lines.Select(l => (IReadOnlyList<string>)[l.Kind, l.Name, Money(l.Amount, order.Currency)]).ToList()));

            blocks.Add(new IncidentFileTable(
                "Status history",
                ["Status", "At"],
                order.StatusHistory.Select(s => (IReadOnlyList<string>)[s.Status, Stamp(s.At)]).ToList()));

            blocks.Add(new IncidentFileTable(
                "Refunds",
                ["Amount", "Reason", "Source", "Status", "Created", "Confirmed"],
                order.Refunds.Select(r => (IReadOnlyList<string>)
                [
                    Money(r.Amount, r.Currency), r.Reason, r.Source, r.Status, Stamp(r.CreatedOn), Stamp(r.ConfirmedOn),
                ]).ToList()));
        }

        return new IncidentFileSection(OrdersTitle, blocks);
    }

    private static IncidentFileSection Disputes(IReadOnlyList<IncidentFileDispute> disputes)
    {
        var blocks = new List<IncidentFileBlock>();
        if (disputes.Count == 0)
        {
            blocks.Add(new IncidentFileParagraph("No disputes."));
        }

        foreach (var dispute in disputes)
        {
            blocks.Add(new IncidentFileSubheading($"Dispute {dispute.Id} on order {dispute.OrderNumber}"));
            blocks.Add(new IncidentFileFieldList(
            [
                new("Filed", Stamp(dispute.CreatedOn)),
                new("Reason", dispute.Reason),
                new("Status", dispute.Status),
                new("Description", dispute.Description),
                new("Evidence files", dispute.EvidenceFileNames.Count == 0 ? Empty : string.Join("; ", dispute.EvidenceFileNames)),
                new("Resolution", Text(dispute.ResolutionNotes)),
                new("Refund", dispute.RefundAmount is null ? Empty : dispute.RefundAmount.Value.ToString("N2", Invariant)),
                new("Resolved by", Text(dispute.ResolvedBy)),
                new("Resolved on", Stamp(dispute.ResolvedOn)),
            ]));

            blocks.Add(new IncidentFileTable(
                "Messages",
                ["At", "Author", "Message"],
                dispute.Messages.Select(m => (IReadOnlyList<string>)[Stamp(m.At), $"{m.AuthorRole} {m.AuthorId}", m.Text]).ToList()));
        }

        return new IncidentFileSection(DisputesTitle, blocks);
    }

    private static IncidentFileSection Consents(IReadOnlyList<IncidentFileConsent> consents)
    {
        var rows = consents.Select(c => (IReadOnlyList<string>)
        [
            c.Type,
            Text(c.DocumentVersion),
            c.EffectiveFrom is null ? Empty : c.EffectiveFrom.Value.ToString("yyyy-MM-dd", Invariant),
            c.IsGranted ? "granted" : "withdrawn",
            Stamp(c.GrantedAt),
            Stamp(c.WithdrawnAt),
            Text(c.IpAddress),
            Text(c.UserAgent),
        ]).ToList();

        IncidentFileBlock block = rows.Count == 0
            ? new IncidentFileParagraph("No consents.")
            : new IncidentFileTable(null, ["Type", "Version", "Effective", "State", "Granted", "Withdrawn", "IP", "User agent"], rows);

        return new IncidentFileSection(ConsentsTitle, [block]);
    }

    private static IncidentFileSection Trail(IncidentFilePdfData data)
    {
        var blocks = new List<IncidentFileBlock>();
        if (data.TrailTruncated)
        {
            blocks.Add(new IncidentFileParagraph("The trail was cut at the newest rows of each source; older rows exist and are not printed."));
        }

        var grouped = new[] { "Customer", "Admin", "Cleaner" }
            .Select(source => (Source: source, Entries: data.Trail.Where(e => e.Source == source).OrderByDescending(e => e.OccurredOn).ToList()));

        foreach (var (source, entries) in grouped)
        {
            blocks.Add(new IncidentFileSubheading($"{source} acts ({entries.Count})"));
            if (entries.Count == 0)
            {
                blocks.Add(new IncidentFileParagraph("None."));
                continue;
            }

            foreach (var entry in entries)
            {
                var fields = new List<IncidentFileField>
                {
                    new("When", Stamp(entry.OccurredOn)),
                    new("Who", entry.ActorId is null ? entry.ActorRole : $"{entry.ActorRole} {entry.ActorId}"),
                    new("Action", entry.Action),
                    new("Outcome", entry.Success ? "success" : $"refused: {Text(entry.ErrorCode)}"),
                    new("Resource", entry.ResourceType is null ? Empty : $"{entry.ResourceType} {Text(entry.ResourceId)}"),
                };
                if (entry.IpAddress is not null || entry.DeviceLabel is not null)
                {
                    fields.Add(new("Request", $"{Text(entry.IpAddress)} / {Text(entry.DeviceLabel)}"));
                }

                blocks.Add(new IncidentFileFieldList(fields));
                if (entry.Evidence.Count > 0)
                {
                    blocks.Add(new IncidentFileTable(
                        null,
                        ["Evidence", "Value"],
                        entry.Evidence.Select(f => (IReadOnlyList<string>)[f.Key, f.Value]).ToList()));
                }
            }
        }

        return new IncidentFileSection(TrailTitle, blocks);
    }

    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? Empty : value;

    private static string Money(decimal amount, string currency) => $"{amount.ToString("N2", Invariant)} {currency}";

    public static string Stamp(DateTimeOffset? at) =>
        at is null ? Empty : at.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", Invariant);

    // Order clocks are unspecified-kind UTC values (the platform's convention for DateTime columns).
    public static string Stamp(DateTime? at) =>
        at is null ? Empty : at.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", Invariant);
}
