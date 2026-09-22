using System.Reflection;
using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Features.CompanyLifecycle.Archive;

namespace Cleansia.Tests.Features.CompanyLifecycle;

/// <summary>
/// ADR-0064 D3 (TC-LC-ARCH-5) — a bundle row carries what the retention regime would leave on the
/// live row plus the ids the books cross-reference by, and never the person: no contact detail, no
/// address line, no bank account, no free text, no secret. The rule lives on the record types, so
/// this walks every one of them, recursing into nested rows, and fails on a member whose name could
/// hold any of it. The guard must refuse the shapes it exists for, or a green run proves nothing.
/// </summary>
public sealed class CompanyArchiveRecordGuardTests
{
    /// <summary>Shared with <c>WorkContractFactsPiiGuardTests</c>: the facts frozen on an acceptance row are held to the same words.</summary>
    internal static readonly string[] ForbiddenNameParts =
    [
        "Email", "Phone", "Passport", "Nationality", "EmergencyContact", "AccessInstructions", "SpecialInstructions",
        "Notes", "Description", "ResolutionNotes", "Secret", "Password", "Ip", "Device", "Token", "Message", "Evidence",
        "Review", "CustomerName", "FirstName", "LastName", "HolderName", "Street", "HouseNumber", "PostalCode", "BirthDate",
        "Iban", "AccountNumber", "BankCode", "Swift",
    ];

    /// <summary>The ADR names the fields <c>Order.AnonymizeCustomerData()</c> blanks; the order row carries none of them.</summary>
    private static readonly string[] AnonymisedOrderMembers =
    [
        "CustomerName", "CustomerEmail", "CustomerPhone", "UserId", "PromoCodeId", "MembershipPlanIdAtPurchase",
        "PreferredEmployeeId", "RecurringTemplateId", "Notes", "SpecialInstructions", "AccessInstructions", "CompletionNotes",
    ];

    private static IReadOnlyList<Type> ShippedRecords() =>
        typeof(CompanyArchiveRecords)
            .GetNestedTypes(BindingFlags.Public)
            .Where(t => t.IsClass)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<(Type Owner, PropertyInfo Member)> MembersOf(IEnumerable<Type> roots)
    {
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>(roots);
        var members = new List<(Type, PropertyInfo)>();
        while (queue.TryDequeue(out var type))
        {
            if (!seen.Add(type))
            {
                continue;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                members.Add((type, property));
                var element = ElementTypeOf(property.PropertyType);
                if (IsNestedRow(element))
                {
                    queue.Enqueue(element);
                }
            }
        }

        return members;
    }

    private static bool IsNestedRow(Type type) =>
        !type.IsEnum && !type.IsPrimitive && type != typeof(string)
        && type.Namespace?.StartsWith("System", StringComparison.Ordinal) != true;

    private static Type ElementTypeOf(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string))
        {
            return type;
        }

        var enumerable = type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable is null ? type : ElementTypeOf(enumerable.GetGenericArguments()[0]);
    }

    /// <summary>
    /// A forbidden part matches anywhere in the name, so a plural or a suffix (<c>Emails</c>,
    /// <c>Messages</c>, <c>Tokens</c>) is caught as the ADR rule says. <c>Ip</c> alone matches as a
    /// PascalCase word: it is a syllable of the books' own vocabulary — Rece<b>ip</b>t, Z<b>ip</b>,
    /// Str<b>ip</b>e, Membersh<b>ip</b> — while every IP-address member in the domain is the word
    /// (<c>IpAddress</c>, <c>ClientIp</c>).
    /// </summary>
    internal static bool IsForbidden(string memberName) =>
        ForbiddenNameParts.Any(part => part == "Ip"
            ? Words(memberName).Any(word => string.Equals(word, part, StringComparison.OrdinalIgnoreCase))
            : memberName.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> Words(string name) =>
        Regex.Split(name, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])").Where(w => w.Length > 0);

    private static IReadOnlyList<string> Offenders(IEnumerable<Type> roots) =>
        MembersOf(roots)
            .Where(m => IsForbidden(m.Member.Name))
            .Select(m => $"{m.Owner.Name}.{m.Member.Name}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void No_Shipped_Bundle_Row_Carries_A_Member_Named_For_A_Person()
    {
        var offenders = Offenders(ShippedRecords());

        Assert.True(offenders.Count == 0,
            "ADR-0064 D3: a bundle row is the books, never the person. Drop the member or record an id:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void The_Order_Row_Carries_Nothing_The_Two_Year_Sweep_Blanks()
    {
        var carried = typeof(CompanyArchiveRecords.Order)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .Where(name => AnonymisedOrderMembers.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(carried);
    }

    [Fact]
    public void The_Guard_Sees_Every_Shipped_Row_Type_And_Recurses_Into_Nested_Rows()
    {
        var roots = ShippedRecords();
        var walked = MembersOf(roots).Select(m => m.Owner).Distinct().ToList();

        Assert.InRange(roots.Count, 20, 40);
        Assert.Contains(typeof(CompanyArchiveRecords.Order), roots);
        Assert.Contains(typeof(CompanyArchiveRecords.WorkContractAcceptance), roots);
        Assert.Contains(typeof(CompanyArchiveRecords.Manifest), roots);
        Assert.Contains(typeof(CompanyArchiveRecords.OrderExtra), walked);
        Assert.Contains(typeof(CompanyArchiveRecords.DisputeLine), walked);
        Assert.Contains(typeof(CompanyArchiveRecords.ManifestFile), walked);
        // Out of the bundle by name (ADR-0064 D3): the person's estate stays under the retention regime.
        foreach (var excluded in new[] { "User", "UserConsent", "CustomerActionAudit", "EmployeePayoutDetails", "UserMembership" })
        {
            Assert.DoesNotContain(roots, t => t.Name == excluded);
        }
    }

    /// <summary>
    /// ADR-0068 D5: the contract record is books — the act, the seat, the cleaner's id, the text, the
    /// version, the instant, the client and the frozen facts — and never the request trio the retention
    /// sweep blanks; the orders row names the text the job was booked under. Pinned by name so the
    /// stream cannot quietly gain the trio under a name the token list does not catch.
    /// </summary>
    [Fact]
    public void The_Contract_Row_Carries_The_Act_And_The_Facts_And_Never_The_Request_Trio_And_The_Order_Row_Names_Its_Text()
    {
        var contract = typeof(CompanyArchiveRecords.WorkContractAcceptance)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
            ["AcceptedOn", "ClientAudience", "DocumentVersion", "EmployeeId", "FactsJson", "Id", "LegalDocumentTextId", "OrderEmployeeId", "OrderId"],
            contract);
        Assert.Contains("WorkContractDocumentId", typeof(CompanyArchiveRecords.Order).GetProperties().Select(p => p.Name));
    }

    /// <summary>
    /// Only the ledger names the person, as its counterparty, and the ADR is silent on it; every
    /// other row cross-references by order, receipt, invoice, dispute or employee id. The order row
    /// is what the two-year sweep leaves, and the dispute row must not restore the link the sweep
    /// broke on the order it belongs to.
    /// </summary>
    [Fact]
    public void Only_The_Ledger_Rows_Name_The_Person_By_Id()
    {
        var rowsNamingTheUser = MembersOf(ShippedRecords())
            .Where(m => m.Member.Name == "UserId")
            .Select(m => m.Owner.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["CreditAccount", "PromoCodeRedemption"], rowsNamingTheUser);
    }

    [Theory]
    [InlineData(typeof(WithEmail), "WithEmail.CustomerEmail")]
    [InlineData(typeof(WithEmails), "WithEmails.Emails")]
    [InlineData(typeof(WithPhones), "WithPhones.Phones")]
    [InlineData(typeof(WithMessages), "WithMessages.Messages")]
    [InlineData(typeof(WithIban), "WithIban.Iban")]
    [InlineData(typeof(WithUpperCaseIban), "WithUpperCaseIban.IBAN")]
    [InlineData(typeof(WithClientIp), "WithClientIp.ClientIp")]
    [InlineData(typeof(WithIpAddress), "WithIpAddress.IpAddress")]
    [InlineData(typeof(WithNestedStreet), "StreetLine.Street")]
    public void The_Guard_Refuses_A_Row_Carrying_The_Shapes_It_Exists_For(Type offender, string expected)
    {
        var offenders = Offenders([offender]);

        Assert.Equal([expected], offenders);
    }

    [Fact]
    public void The_Guard_Passes_A_Row_Of_Ids_Money_Enums_And_The_Books_Own_Vocabulary()
    {
        Assert.Empty(Offenders([typeof(Clean)]));
    }

    private sealed record WithEmail(string Id, string CustomerEmail);

    private sealed record WithEmails(string Id, string Emails);

    private sealed record WithPhones(string Id, string Phones);

    private sealed record WithMessages(string Id, string Messages);

    private sealed record WithIpAddress(string Id, string IpAddress);

    private sealed record WithIban(string Id, string Iban);

    private sealed record WithUpperCaseIban(string Id, string IBAN);

    private sealed record WithClientIp(string Id, string ClientIp);

    private sealed record StreetLine(string Street);

    private sealed record WithNestedStreet(string Id, IReadOnlyList<StreetLine> Lines);

    private sealed record Clean(
        string Id,
        string OrderId,
        string ReceiptId,
        string ReceiptNumber,
        string ZipCode,
        string StripePaymentIntentId,
        decimal MembershipDiscountAmount,
        decimal Amount,
        Cleansia.Core.Domain.Enums.RefundReason Reason,
        DateTimeOffset CreatedOn);
}
