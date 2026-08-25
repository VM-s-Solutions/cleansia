using System.Text.RegularExpressions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Tests.Logging;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// Every table that holds a data subject's identifier, with a written verdict on what a GDPR erasure does
/// to it — the table-side twin of <see cref="BlobContainerErasureRosterTests"/>, and built for the same
/// reason that one was.
///
/// <para><b>Why this exists.</b> Three tables were found by hand, one at a time, in a single sprint:
/// <c>DeadLetter</c>, <c>OutboxMessage</c>, <c>LiveActivityToken</c>. None of them was a decision anyone had
/// made — each was simply a table nobody had enumerated, exactly as dispute-evidence blobs were. Finding
/// the fourth by hand is a matter of luck, and luck is not a control. Building this roster found four
/// more in one pass: a logged-out <c>Device</c> tombstone (which the erasure's <c>IsActive</c> read and the
/// stale-device sweep BOTH filtered away), every <c>EmployeeDocument</c> row whose blob the erasure had
/// already deleted, the <c>OrderPhoto</c> free text the order aggregate's own anonymization walk never
/// reached, and a live <c>RefreshToken</c> whose retention clock the erasure never started.</para>
///
/// <para><b>The corpus is the EF model, never a hand-typed list</b> — that is the whole point. A new entity
/// with a <c>UserId</c> appears here automatically and reddens until somebody writes a verdict for it, which
/// is the case that matters; a verdict removed is the easy half.</para>
///
/// <para><b>What puts a table in the corpus</b> — five structural questions, no name list. Four ask whether
/// it holds an identifier: a foreign key whose principal is <see cref="User"/> or <see cref="Employee"/>; a
/// scalar whose name ends in <c>UserId</c>/<c>EmployeeId</c> (an unmapped actor reference —
/// <c>ReviewedByUserId</c>, <c>ReportedByEmployeeId</c>); a scalar matching the LIVE contact-identity shape
/// the request-logging middleware redacts by (read out of the compiled regex, so the two cannot drift); or a
/// postal-address scalar. That last family is what puts <c>Address</c> in the corpus — a home address with
/// no subject id at all.</para>
///
/// <para><b>The fifth question is the one the first four would have failed, and it is why it is here.</b>
/// <c>DeadLetter</c> and <c>OutboxMessage</c> carry no subject column whatsoever: their handle is inside a
/// serialized wire body. An identifier walk would have found neither — the two tables that were actually
/// missed. So the corpus also takes any entity DECLARING an unbounded text column (no max length, not a key),
/// on the reasoning that an unbounded text column is where a serialized payload lives. Measured: that adds
/// exactly those two and nothing else, and restricting it to columns the entity declares itself is what keeps
/// an inherited audit string from sweeping in the whole schema.</para>
///
/// <para><b>The boundary, stated rather than implied.</b> This walk sees a table's OWN columns. A child row
/// that reaches its subject only through a parent — <c>CartServiceItem</c>, <c>OrderService</c>,
/// <c>OrderStatusTrack</c>, <c>LoyaltyTransaction</c>, <c>DisputeEvidence</c> — is out of the corpus and is
/// covered by its parent's verdict and the database's own cascade. Widening to transitive reachability would
/// pull in most of the schema and classify nothing. What remains genuinely out of reach is a subject handle
/// hidden in a LENGTH-CAPPED column — a user id packed into a 256-char key would be invisible to all five
/// questions — and no column walk closes that.</para>
///
/// <para><b>One tenancy note, because three erasure walks now carry a comment about it.</b> The erasure
/// loads its subject through a TENANT-SCOPED read (<c>userRepository.GetQueryable()</c>), so it can only run
/// at all when the caller's tenant already matches the subject's. A row stamped with the subject's own
/// tenant is therefore reachable either way, and the <c>IgnoreQueryFilters</c> in those walks is defence in
/// depth rather than a live bug fix. The one row where it is load-bearing is <c>DeadLetter</c>: a poison
/// consumer on the Functions host stamps whatever tenant was ambient there, which is nobody's in
/// particular.</para>
/// </summary>
public class SubjectDataErasureRosterTests
{
    public enum Verdict
    {
        /// <summary>The erasure deletes the rows.</summary>
        Deleted,

        /// <summary>The erasure keeps the row and blanks the subject's fields in it.</summary>
        AnonymizedInPlace,

        /// <summary>Kept, under a named ruling or a named retention window. The reason says which.</summary>
        RetainedByPolicy,

        /// <summary>
        /// Kept because the payload identifies a device, a quota slot or a counter rather than a person, and
        /// its only subject handle is an id that no longer resolves to one.
        ///
        /// <para><b>This verdict does not settle the question on its own, and T-0587 is why.</b>
        /// <c>LiveActivityToken</c>'s payload is pseudonymous by exactly this test and it is still erased —
        /// because its sibling <c>Device</c> row, holding the same handset's identity, was already being
        /// deleted, and two tables recording one fact with one erased and one not is an inconsistency
        /// whatever class the payload falls into. Use this verdict only where no sibling table is treated
        /// differently.</para>
        /// </summary>
        RetainedPseudonymous,

        /// <summary>
        /// The matched member is platform or tenant configuration, not a person's data — a company's own
        /// contact details, a country's dialling prefix.
        /// </summary>
        NotADataSubject
    }

    /// <summary>A file and a symbol inside it that performs this verdict — checked to still be there.</summary>
    public record Site(string File, string Symbol);

    public record Row(Verdict Verdict, string Why, params Site[] Sites);

    private const string ErasureService = "Cleansia.Core.AppServices/Services/GdprDeletionService.cs";
    private const string OrderAggregate = "Cleansia.Core.Domain/Orders/Order.cs";
    private const string DisputeAggregate = "Cleansia.Core.Domain/Disputes/Dispute.cs";

    private static Site InErasure(string symbol) => new(ErasureService, symbol);

    private static readonly Dictionary<Type, Row> Roster = new()
    {
        [typeof(User)] = new(
            Verdict.AnonymizedInPlace,
            "The subject row itself. Kept and blanked rather than deleted so every retained order, invoice "
                + "and audit row keeps a resolvable foreign key.",
            InErasure("user.Anonymize()"), InErasure("user.Deactivated(")),

        [typeof(Employee)] = new(
            Verdict.AnonymizedInPlace,
            "Same reason as User, and the erasure refuses to run while a pay-period invoice is open.",
            InErasure("user.Employee.Anonymize()")),

        [typeof(Address)] = new(
            Verdict.AnonymizedInPlace,
            "A home address with no subject id of its own — reached through its two parents.",
            InErasure("order.CustomerAddress?.Anonymize()"), InErasure("user.Employee.Address?.Anonymize()")),

        [typeof(Core.Domain.Orders.Order)] = new(
            Verdict.AnonymizedInPlace,
            "The service and financial record. Kept, with the customer's name, address, phone, notes and "
                + "instructions blanked in place.",
            InErasure("order.AnonymizeCustomerData()")),

        [typeof(Core.Domain.Orders.OrderReview)] = new(
            Verdict.AnonymizedInPlace,
            "Blanked inside the order aggregate's own walk, not by a separate call.",
            new Site(OrderAggregate, "review.Anonymize()")),

        [typeof(Core.Domain.Orders.OrderNote)] = new(
            Verdict.AnonymizedInPlace,
            "Blanked inside the order aggregate's own walk.",
            new Site(OrderAggregate, "note.Anonymize()")),

        [typeof(Core.Domain.Orders.OrderIssue)] = new(
            Verdict.AnonymizedInPlace,
            "Blanked inside the order aggregate's own walk.",
            new Site(OrderAggregate, "issue.Anonymize()")),

        [typeof(Core.Domain.Orders.OrderPhoto)] = new(
            Verdict.AnonymizedInPlace,
            "The blob goes; the row stays because the order does. Its uploader-supplied file name and free "
                + "text are blanked next to the blob delete — they were outside the aggregate's walk, since "
                + "photos are not a navigation on the order it loads.",
            InErasure("photo.Anonymize()")),

        [typeof(Core.Domain.Disputes.Dispute)] = new(
            Verdict.AnonymizedInPlace,
            "The adjudication record is kept; the subject's text and the evidence paths are blanked, in that "
                + "order relative to the blob deletes.",
            InErasure("dispute.Anonymize()")),

        [typeof(Core.Domain.Disputes.DisputeMessage)] = new(
            Verdict.AnonymizedInPlace,
            "Blanked inside the dispute aggregate's own walk.",
            new Site(DisputeAggregate, "message.Anonymize()")),

        [typeof(Core.Domain.EmployeePayroll.OrderEmployeePay)] = new(
            Verdict.AnonymizedInPlace,
            "A pay row is a financial record and is kept; its subject reference is blanked.",
            InErasure("pay.Anonymize()")),

        [typeof(Core.Domain.Users.Cart)] = new(
            Verdict.Deleted, "Working state with no record value.", InErasure("cartRepository.Remove(user.Cart)")),

        [typeof(Core.Domain.Users.SavedAddress)] = new(
            Verdict.Deleted, "The subject's own address book.", InErasure("savedAddressRepository.RemoveRange")),

        [typeof(Core.Domain.Bookings.RecurringBookingTemplate)] = new(
            Verdict.Deleted,
            "A standing instruction to create future orders for an account that no longer exists.",
            InErasure("recurringBookingTemplateRepository.RemoveRange")),

        [typeof(Core.Domain.Notifications.UserNotification)] = new(
            Verdict.Deleted, "The subject's own feed.", InErasure("userNotificationRepository.RemoveRange")),

        [typeof(Core.Domain.Devices.Device)] = new(
            Verdict.Deleted,
            "Handset identity and a push token. EVERY row, active or not: a logged-out tombstone stays "
                + "physically present so a later login can reclaim it, and the stale-device retention sweep "
                + "filters on IsActive as well — so it was reachable by neither path.",
            InErasure("deviceRepository.RemoveForSubjectAsync")),

        [typeof(Core.Domain.LiveActivities.LiveActivityToken)] = new(
            Verdict.Deleted,
            "The same handset's other APNs address. Pseudonymous by payload and erased anyway — see the "
                + "RetainedPseudonymous verdict's own note.",
            InErasure("liveActivityTokenRepository.RemoveForSubjectAsync")),

        [typeof(Core.Domain.Users.RefreshToken)] = new(
            Verdict.RetainedByPolicy,
            "Kept for the forensic window RefreshTokenCleanupService names, which hard-deletes tokens that "
                + "are revoked OR expired and at least 90 days old. The erasure REVOKES rather than deletes, "
                + "which is what starts that clock: an untouched live token would otherwise keep the "
                + "subject's IP address, device label and device id until its own natural expiry first. Not a "
                + "session cut — the refresh path already refuses a deactivated user — and ADR-0027's poll "
                + "predicate is untouched, since it reads the password_reset reason alone.",
            InErasure("refreshTokenService.RevokeAllForUserAsync")),

        [typeof(Core.Domain.Documents.DocumentDeletionRequest)] = new(
            Verdict.Deleted,
            "The subject's own words, in the reason they wrote for wanting a document removed, plus the "
                + "admin's answer to it. Neither survives a person who no longer exists. The rows are also "
                + "structurally unable to be kept: the FK to EmployeeDocument is Restrict and the erasure "
                + "deletes those documents, so a surviving request would not leave residue — it would make "
                + "the whole erasure throw. Removed BEFORE the documents for exactly that reason.",
            InErasure("documentDeletionRequestRepository.RemoveForEmployeeAsync")),

        [typeof(Core.Domain.Documents.EmployeeDocument)] = new(
            Verdict.Deleted,
            "The erasure already deletes every one of these blobs, so the platform has decided the document "
                + "does not survive. The rows went with them: file name (routinely the person's own), path, "
                + "description and reviewer free text, pointing at bytes that no longer exist. The "
                + "superseded-document purge could not reach them — it takes only already-deactivated rows.",
            InErasure("employeeDocumentRepository.RemoveForEmployeeAsync")),

        [typeof(EmployeePayoutDetails)] = new(
            Verdict.Deleted,
            "ADR-0034 D1.1.2 — id-keyed, because reaching through Employee.PayoutDetails is a silent no-op "
                + "on the aggregate this service loads.",
            InErasure("employeePayoutDetailsRepository.RemoveForEmployeeAsync")),

        [typeof(Core.Domain.DeadLettering.DeadLetter)] = new(
            Verdict.Deleted,
            "A poisoned send-email body is the wire payload verbatim: address, real name and a live token.",
            InErasure("deadLetterRepository.RemoveForSubjectAsync")),

        [typeof(Core.Domain.Outbox.OutboxMessage)] = new(
            Verdict.Deleted,
            "The same body one table over, before it poisons anything, in every status — the prune refuses "
                + "Pending and Failed rows by design.",
            InErasure("outboxMessageRepository.RemoveForSubjectAsync")),

        [typeof(Core.Domain.Users.UserConsent)] = new(
            Verdict.RetainedByPolicy,
            "The consent ledger is the lawful-basis record, so the erasure withdraws rather than deletes; "
                + "the withdrawn row is then swept on the retention job's withdrawn-consents window.",
            InErasure("consent.Withdraw()")),

        [typeof(Core.Domain.Users.GdprRequest)] = new(
            Verdict.RetainedByPolicy,
            "It IS the erasure's own audit record — deleting it would erase the evidence that the erasure "
                + "happened. The retention job anonymizes its ProcessedBy after its own window.",
            InErasure("gdprRequestRepository.Add(auditEntry)")),

        [typeof(Core.Domain.Auditing.AdminActionAudit)] = new(
            Verdict.RetainedByPolicy,
            "ADR-0012 append-only accountability. Its ActorEmail is the ADMIN who acted, not the erased "
                + "subject — unless the erased subject is themselves an admin, in which case their actor "
                + "rows are retained on the same ADR-0012 ground."),

        [typeof(Core.Domain.EmployeePayroll.EmployeeInvoice)] = new(
            Verdict.RetainedByPolicy,
            "ADR-0007 D4 financial record. The erasure refuses to run at all while one is Pending, Approved "
                + "or Disputed."),

        [typeof(Core.Domain.Memberships.UserMembership)] = new(
            Verdict.RetainedByPolicy,
            "A subscription record with its Stripe identifiers — a financial record on the same ADR-0007 D4 "
                + "ground. The erasure requests cancellation at period end rather than deleting the row.",
            InErasure("membership.MarkCancellationRequested()")),

        [typeof(Core.Domain.EmployeePayroll.EmployeePayConfig)] = new(
            Verdict.RetainedPseudonymous,
            "Pay rates keyed to an employee id that no longer resolves to a person; no name, contact or "
                + "free text. Deleting them would rewrite the basis of retained pay rows."),

        [typeof(Core.Domain.Orders.OrderEmployee)] = new(
            Verdict.RetainedPseudonymous,
            "Crew assignment on a retained order — two ids and nothing else."),

        [typeof(Core.Domain.Loyalty.LoyaltyAccount)] = new(
            Verdict.RetainedPseudonymous,
            "A points balance and tier keyed to an anonymized user id."),

        [typeof(Core.Domain.Loyalty.Referral)] = new(
            Verdict.RetainedPseudonymous,
            "A TWO-PARTY ledger: deleting the erased party's row would destroy the counterparty's record of "
                + "points they earned, and remove the Status the referral idempotency check reads."),

        [typeof(Core.Domain.Loyalty.ReferralCode)] = new(
            Verdict.RetainedPseudonymous,
            "A code and a user id; TimesUsed is a counterparty-facing counter."),

        [typeof(Core.Domain.Loyalty.PromoCodeRedemption)] = new(
            Verdict.RetainedPseudonymous,
            "One row is one consumed slot — it is the per-user cap's arbiter, and deleting it hands the cap "
                + "back."),

        [typeof(Core.Domain.Memberships.MembershipBenefitUsage)] = new(
            Verdict.RetainedPseudonymous,
            "ADR-0035 quota ledger, keyed on (tenant, user, kind, period). Deleting a row resets a quota "
                + "that was genuinely consumed."),

        [typeof(Core.Domain.Notifications.UserNotificationPreferences)] = new(
            Verdict.RetainedPseudonymous,
            "Booleans keyed to an anonymized user id."),

        [typeof(Core.Domain.Messaging.CampaignProgress)] = new(
            Verdict.RetainedPseudonymous,
            "A fan-out resume cursor. LastProcessedUserId is a position in a sweep, not a record about the "
                + "person it names."),

        [typeof(Core.Domain.Company.CompanyInfo)] = new(
            Verdict.NotADataSubject,
            "The platform's / tenant's own invoicing contact details."),

        [typeof(Core.Domain.Configuration.CountryConfiguration)] = new(
            Verdict.NotADataSubject,
            "PhonePrefix is a country dialling code."),
    };

    /// <summary>
    /// The addition guard, and the one this roster exists for. A new entity carrying a subject identifier
    /// appears in the model walk on its own and has to be given a verdict before this passes.
    /// </summary>
    [Fact]
    public void Every_Subject_Bearing_Entity_In_The_Model_Carries_A_Verdict()
    {
        var walked = SubjectBearingEntities();

        Assert.True(walked.Count >= 32,
            $"The model walk classified only {walked.Count} entity types as carrying subject data. It found "
            + "38 when written, so a number this low means the predicate stopped matching — not that the "
            + "schema shrank — and every assertion here would then be vacuous.");

        var missing = walked.Except(Roster.Keys).Select(t => t.Name).Order().ToList();
        var stale = Roster.Keys.Except(walked).Select(t => t.Name).Order().ToList();

        Assert.True(missing.Count == 0,
            "These entity types carry a data-subject identifier and no erasure verdict: "
            + string.Join(", ", missing)
            + ". Add a row to the roster saying what the erasure does to them and why — that decision is the "
            + "point of this test, and leaving it unmade is how three tables were missed one at a time.");

        Assert.True(stale.Count == 0,
            "These roster rows name entity types that no longer carry a subject identifier (or no longer "
            + "exist): " + string.Join(", ", stale));
    }

    /// <summary>
    /// The drift guard: a verdict and the code that performs it cannot part company silently. Each site is
    /// a file plus a symbol read out of that file, so removing the call reddens the verdict that claims it —
    /// including the ones inside an aggregate's own walk, which <c>GdprDeletionService</c> never names.
    /// </summary>
    [Fact]
    public void Every_Verdict_That_Claims_An_Action_Names_A_Site_That_Still_Performs_It()
    {
        var sources = new Dictionary<string, string>();
        var checkedSites = 0;

        foreach (var (type, row) in Roster)
        {
            foreach (var site in row.Sites)
            {
                if (!sources.TryGetValue(site.File, out var source))
                {
                    var path = Path.Combine(SourceRoot().FullName, site.File.Replace('/', Path.DirectorySeparatorChar));
                    Assert.True(File.Exists(path), $"{type.Name}'s verdict names a file that does not exist: {site.File}");
                    source = File.ReadAllText(path);
                    sources[site.File] = source;
                }

                Assert.True(source.Contains(site.Symbol, StringComparison.Ordinal),
                    $"{type.Name} is on the roster as {row.Verdict}, but {site.File} no longer contains "
                    + $"'{site.Symbol}'. Either restore the call or change the verdict — a roster that says a "
                    + "table is erased while nothing erases it is worse than no roster.");
                checkedSites++;
            }
        }

        Assert.True(checkedSites >= 22,
            $"Only {checkedSites} action sites were checked; there were 27 when this was written, so the "
            + "roster has lost its sites rather than this guard having nothing to do.");
    }

    /// <summary>
    /// Roster well-formedness, so a row cannot claim an action without naming one or state a retention
    /// without saying on what ground.
    /// </summary>
    [Fact]
    public void Acting_Verdicts_Name_A_Site_And_Every_Row_States_Its_Reason()
    {
        foreach (var (type, row) in Roster)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Why), $"{type.Name} carries no reason.");

            if (row.Verdict is Verdict.Deleted or Verdict.AnonymizedInPlace)
            {
                Assert.True(row.Sites.Length > 0,
                    $"{type.Name} claims the erasure acts on it ({row.Verdict}) but names no site that does.");
            }
        }
    }

    private static readonly Regex LooseSubjectId =
        new("(UserId|EmployeeId)$", RegexOptions.Compiled);

    /// <summary>
    /// A postal address has no subject id and no contact-shaped name, so the shape the middleware redacts by
    /// cannot see it. Verified when written to add exactly one entity type to the corpus (<c>Address</c>);
    /// <c>CompanyInfo</c> also matches and was already in via its own contact scalars.
    /// </summary>
    private static readonly Regex PostalAddressField =
        new("^(street|[A-Za-z]*zipCode|[A-Za-z]*postalCode|houseNumber|apartment|latitude|longitude)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static IReadOnlyCollection<Type> SubjectBearingEntities()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new RosterTenantProvider());

        var entityTypes = context.Model.GetEntityTypes().ToList();
        Assert.True(entityTypes.Count >= 60,
            $"The EF model exposed only {entityTypes.Count} entity types; the walk cannot be trusted.");

        var contactShapes = WireSurface.ReadContactIdentityTokens();
        Assert.NotEmpty(contactShapes);

        return entityTypes
            .Where(entityType => IsSubjectBearing(entityType, contactShapes) || CarriesUnboundedText(entityType))
            .Select(entityType => entityType.ClrType)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// The payload-carrier question, and the one the identifier questions would have failed:
    /// <c>DeadLetter</c> and <c>OutboxMessage</c> hold their subject inside a serialized wire body and carry
    /// no subject column at all. Declared-on-this-entity is load-bearing — an inherited audit string is
    /// unbounded on nearly every table and would classify the whole schema.
    /// </summary>
    private static bool CarriesUnboundedText(IEntityType entityType) =>
        entityType.GetProperties().Any(p =>
            p.ClrType == typeof(string) && p.GetMaxLength() is null && !p.IsForeignKey() && !p.IsPrimaryKey()
            && p.PropertyInfo?.DeclaringType == entityType.ClrType);

    private static bool IsSubjectBearing(IEntityType entityType, IReadOnlyList<string> contactShapes) =>
        entityType.GetForeignKeys().Any(fk =>
            fk.PrincipalEntityType.ClrType == typeof(User) || fk.PrincipalEntityType.ClrType == typeof(Employee))
        || entityType.GetProperties().Any(property =>
            LooseSubjectId.IsMatch(property.Name)
            || PostalAddressField.IsMatch(property.Name)
            || contactShapes.Any(shape =>
                Regex.IsMatch(property.Name, $"^(?:{shape})$", RegexOptions.IgnoreCase)));

    private static DirectoryInfo SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.EnumerateFiles("Cleansia.Api.sln").Any())
        {
            dir = dir.Parent;
        }

        Assert.True(
            dir is not null,
            $"Could not find Cleansia.Api.sln walking up from {AppContext.BaseDirectory}. "
                + "If the solution moved, update this test.");

        return dir!;
    }

    private sealed class RosterTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
