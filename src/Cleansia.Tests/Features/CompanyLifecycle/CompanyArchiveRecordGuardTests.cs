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
    private static readonly string[] ForbiddenNameParts =
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
    /// A forbidden part matches on PascalCase word boundaries, not on raw substrings: <c>Ip</c> is the
    /// word in <c>ClientIp</c> and <c>IpAddress</c>, not the letters inside <c>ReceiptNumber</c> or
    /// <c>ZipCode</c> — the ADR's own layout carries a receipt number on every order row.
    /// </summary>
    private static bool IsForbidden(string memberName)
    {
        var words = Words(memberName);
        return ForbiddenNameParts.Select(Words).Any(part =>
            Enumerable.Range(0, Math.Max(0, words.Count - part.Count + 1))
                .Any(start => part.Select((word, i) => string.Equals(words[start + i], word, StringComparison.OrdinalIgnoreCase)).All(x => x)));
    }

    private static List<string> Words(string name) =>
        Regex.Split(name, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])").Where(w => w.Length > 0).ToList();

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

    [Theory]
    [InlineData(typeof(WithEmail), "WithEmail.CustomerEmail")]
    [InlineData(typeof(WithIban), "WithIban.Iban")]
    [InlineData(typeof(WithUpperCaseIban), "WithUpperCaseIban.IBAN")]
    [InlineData(typeof(WithClientIp), "WithClientIp.ClientIp")]
    [InlineData(typeof(WithNestedStreet), "StreetLine.Street")]
    public void The_Guard_Refuses_A_Row_Carrying_The_Shapes_It_Exists_For(Type offender, string expected)
    {
        var offenders = Offenders([offender]);

        Assert.Equal([expected], offenders);
    }

    [Fact]
    public void The_Guard_Passes_A_Row_Of_Ids_Money_Enums_And_A_Receipt_Number()
    {
        Assert.Empty(Offenders([typeof(Clean)]));
    }

    private sealed record WithEmail(string Id, string CustomerEmail);

    private sealed record WithIban(string Id, string Iban);

    private sealed record WithUpperCaseIban(string Id, string IBAN);

    private sealed record WithClientIp(string Id, string ClientIp);

    private sealed record StreetLine(string Street);

    private sealed record WithNestedStreet(string Id, IReadOnlyList<StreetLine> Lines);

    private sealed record Clean(string Id, string OrderId, string ReceiptNumber, string ZipCode, decimal Amount, Cleansia.Core.Domain.Enums.RefundReason Reason, DateTimeOffset CreatedOn);
}
