using System.Reflection;
using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Tests.Logging;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D0/D3 (Verification #5) — a customer audit payload holds identifiers, money, enums,
/// versions, flags and counts, and never the person: no contact detail, no address text, no free text
/// the customer typed. <c>IAuditContext.RecordEvidence</c> takes <c>object</c> and serialises it whole,
/// so nothing at the seam can tell a redacted record from an unredacted one — the discipline has to
/// live on the TYPES, and this walks every one of them, recursing into nested records, by the marker
/// rather than by nesting position (two records are shared and live top-level).
///
/// <para>Reasons are enums and descriptions are lengths: a member named <c>Reason</c> is allowed when it
/// is an enum and refused when it is a string; <c>DescriptionLength</c> is an int and passes because it
/// does not end in <c>Description</c>. The contact-identity names are read off the request-logging
/// middleware's own regex so the two lists cannot drift.</para>
/// </summary>
public sealed class CustomerAuditPayloadPiiGuardTests
{
    private static readonly Regex FreeTextSuffix = new(
        "(reason|description|instructions?|note|notes|comment|message|text)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IdentitySuffix = new(
        "(name|email|phone|address|street|city|zip|zipcode|iban|token|secret|password)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IdentifierShape = new("(Id|Ids)$", RegexOptions.Compiled);

    /// <summary>The only shapes a string member may take: an identifier, a code, a version, or a bounded label.</summary>
    private static readonly Regex AllowedStringMember = new(
        "^(.*Id|.*Ids|.*IdAtPurchase|.*Code|.*Version|.*VersionAccepted|Language|Method|Channel|Tier|Weekday|TimeOfDay|Frequency|.*Slugs|PaymentType|PaymentStatus)$",
        RegexOptions.Compiled);

    private static readonly HashSet<Type> ScalarTypes =
    [
        typeof(bool), typeof(byte), typeof(short), typeof(int), typeof(long), typeof(decimal), typeof(double), typeof(float),
        typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan),
    ];

    private static IReadOnlyList<Type> PayloadRoots() =>
        typeof(ICustomerAuditPayload).Assembly
            .GetTypes()
            .Where(t => typeof(ICustomerAuditPayload).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<(Type Owner, PropertyInfo Member)> AllMembers()
    {
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>(PayloadRoots());
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
                if (element.Assembly == typeof(ICustomerAuditPayload).Assembly && !element.IsEnum)
                {
                    queue.Enqueue(element);
                }
            }
        }

        return members;
    }

    private static Type ElementTypeOf(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string))
        {
            return type;
        }

        if (type.IsArray)
        {
            return ElementTypeOf(type.GetElementType()!);
        }

        var enumerable = type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable is null ? type : ElementTypeOf(enumerable.GetGenericArguments()[0]);
    }

    private static string Describe((Type Owner, PropertyInfo Member) m) =>
        $"{m.Owner.DeclaringType?.Name ?? m.Owner.Namespace!.Split('.')[^1]}.{m.Owner.Name}.{m.Member.Name} : {m.Member.PropertyType.Name}";

    [Fact]
    public void No_Payload_Member_Is_Named_For_A_Contact_Identity_Or_An_Address()
    {
        var offenders = AllMembers()
            .Where(m => !IdentifierShape.IsMatch(m.Member.Name))
            .Where(m => WireSurface.IsRedacted(m.Member.Name) || IdentitySuffix.IsMatch(m.Member.Name))
            .Select(Describe)
            .ToList();

        Assert.True(offenders.Count == 0,
            "ADR-0062 D0: a customer audit row references the person by id and never reproduces them. "
            + "Remove the member or record an identifier instead:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void No_Payload_Member_Carries_The_Customers_Free_Text()
    {
        var offenders = AllMembers()
            .Where(m => FreeTextSuffix.IsMatch(m.Member.Name))
            .Where(m => ElementTypeOf(m.Member.PropertyType) == typeof(string) || !ElementTypeOf(m.Member.PropertyType).IsEnum)
            .Select(Describe)
            .ToList();

        Assert.True(offenders.Count == 0,
            "ADR-0062 A-4: the customer's text stays on the domain row under its own erasure verdict; the "
            + "audit row records that it was given and how long it was. A reason is an enum, a description "
            + "is a length:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Every_String_Member_Is_An_Identifier_A_Code_A_Version_Or_A_Bounded_Label()
    {
        var offenders = AllMembers()
            .Where(m => ElementTypeOf(m.Member.PropertyType) == typeof(string))
            .Where(m => !AllowedStringMember.IsMatch(m.Member.Name))
            .Select(Describe)
            .ToList();

        Assert.True(offenders.Count == 0,
            "ADR-0062 D3: an unbounded string on an evidence record is free text until proven otherwise. "
            + "Name it as an id/code/version, make it an enum, or record its length:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Every_Member_Is_A_Scalar_An_Enum_A_Nested_Record_Or_A_Collection_Of_Those()
    {
        var offenders = AllMembers()
            .Where(m =>
            {
                var element = ElementTypeOf(m.Member.PropertyType);
                return element != typeof(string)
                    && !element.IsEnum
                    && !ScalarTypes.Contains(element)
                    && element.Assembly != typeof(ICustomerAuditPayload).Assembly;
            })
            .Select(Describe)
            .ToList();

        Assert.True(offenders.Count == 0,
            "A payload member of a domain or framework type would serialise whatever that type carries. "
            + "Project it into a nested record of facts:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void No_Payload_Names_The_Preferred_Cleaner()
    {
        var offenders = AllMembers()
            .Where(m => m.Member.Name.Contains("Preferred", StringComparison.OrdinalIgnoreCase)
                        || m.Member.Name.Contains("Employee", StringComparison.OrdinalIgnoreCase))
            .Select(Describe)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Order.AnonymizeCustomerData nulls PreferredEmployeeId on purpose (nobody is ever told they were "
            + "passed over); the audit row must not undo that choice:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>Anti-vacuity: the walk must see the whole family, including the records nested inside a feature and the shared top-level ones.</summary>
    [Fact]
    public void The_Guard_Sees_Every_Evidence_Record_And_Recurses_Into_Nested_Facts()
    {
        var roots = PayloadRoots();
        var walked = AllMembers().Select(m => m.Owner).Distinct().ToList();

        Assert.InRange(roots.Count, 11, 40);
        Assert.Contains(typeof(CancelOrder.OrderCancellationEvidence), roots);
        Assert.Contains(typeof(CreateOrder.OrderBookingEvidence), roots);
        Assert.Contains(typeof(CreateDispute.DisputeFilingEvidence), roots);
        Assert.Contains(typeof(MembershipSubscribeEvidence), roots);
        Assert.Contains(typeof(CancelOrder.CancellationPolicyFigures), walked);
        Assert.Contains(typeof(CreateOrder.CancellationPolicyShown), walked);
        Assert.Contains(typeof(SwapMembershipPlan.MembershipPlanFacts), walked);
    }

    /// <summary>The guard itself must refuse the shapes it exists for, or a green run proves nothing.</summary>
    [Theory]
    [InlineData(nameof(Offender.CustomerEmail), true)]
    [InlineData(nameof(Offender.CustomerPhone), true)]
    [InlineData(nameof(Offender.FirstName), true)]
    [InlineData(nameof(Offender.CustomerAddress), true)]
    [InlineData(nameof(Offender.SavedAddressId), false)]
    [InlineData(nameof(Offender.AddressId), false)]
    public void The_Identity_Rule_Refuses_Contact_And_Address_Names_But_Not_Their_Ids(string member, bool refused)
    {
        var caught = !IdentifierShape.IsMatch(member) && (WireSurface.IsRedacted(member) || IdentitySuffix.IsMatch(member));

        Assert.Equal(refused, caught);
    }

    [Theory]
    [InlineData(nameof(Offender.CancellationReason), typeof(string), true)]
    [InlineData(nameof(Offender.Description), typeof(string), true)]
    [InlineData(nameof(Offender.SpecialInstructions), typeof(string), true)]
    [InlineData(nameof(Offender.Reason), typeof(Cleansia.Core.Domain.Enums.DisputeReason), false)]
    [InlineData(nameof(Offender.DescriptionLength), typeof(int), false)]
    public void The_Free_Text_Rule_Refuses_Text_Members_But_Not_An_Enum_Reason_Or_A_Length(string member, Type type, bool refused)
    {
        var caught = FreeTextSuffix.IsMatch(member) && (type == typeof(string) || !type.IsEnum);

        Assert.Equal(refused, caught);
    }

    [Theory]
    [InlineData("Notes", true)]
    [InlineData("AccessMode", true)]
    [InlineData("PromoCodeId", false)]
    [InlineData("CurrencyCode", false)]
    [InlineData("TermsVersionAccepted", false)]
    [InlineData("ExtraSlugs", false)]
    public void The_String_Rule_Refuses_Anything_Not_Shaped_Like_An_Id_Code_Version_Or_Label(string member, bool refused)
    {
        Assert.Equal(refused, !AllowedStringMember.IsMatch(member));
    }

    private sealed record Offender(
        string CustomerEmail,
        string CustomerPhone,
        string FirstName,
        string CustomerAddress,
        string SavedAddressId,
        string AddressId,
        string CancellationReason,
        string Description,
        string SpecialInstructions,
        Cleansia.Core.Domain.Enums.DisputeReason Reason,
        int DescriptionLength);
}
