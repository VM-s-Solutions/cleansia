using System.Reflection;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Tests.Features.CompanyLifecycle;

namespace Cleansia.Tests.Features.Contracts;

/// <summary>
/// ADR-0068 D2 — the facts frozen on an acceptance row outlive the person, so they may name the job
/// (number, window, price, coarse location, catalogue names) and never the street or the customer. Every
/// member of every <see cref="IWorkContractFacts"/> implementation, recursing into nested records, is
/// held to the archive bundle guard's own word list, so a <c>customerName</c> "because the contract
/// should name the parties" is a red build before it is a review comment.
/// </summary>
public sealed class WorkContractFactsPiiGuardTests
{
    private static IReadOnlyList<Type> Roots() =>
        typeof(IWorkContractFacts).Assembly
            .GetTypes()
            .Where(t => typeof(IWorkContractFacts).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
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
                if (property.Name == "EqualityContract")
                {
                    continue;
                }

                members.Add((type, property));
                var element = ElementTypeOf(property.PropertyType);
                if (element.Assembly == typeof(IWorkContractFacts).Assembly && !element.IsEnum)
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

        var enumerable = type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable is null ? type : ElementTypeOf(enumerable.GetGenericArguments()[0]);
    }

    [Fact]
    public void No_Facts_Member_Is_Named_For_A_Person_Or_A_Street()
    {
        var offenders = MembersOf(Roots())
            .Where(m => CompanyArchiveRecordGuardTests.IsForbidden(m.Member.Name))
            .Select(m => $"{m.Owner.Name}.{m.Member.Name}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "ADR-0068 D2: the facts on an acceptance row name the job, never the person. Drop the member:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void The_Guard_Sees_The_Facts_Record_And_Recurses_Into_Its_Lines()
    {
        var roots = Roots();
        var walked = MembersOf(roots).Select(m => m.Owner).Distinct().ToList();

        Assert.Contains(typeof(WorkContractFacts), roots);
        Assert.Contains(typeof(WorkContractFactsLine), walked);
        Assert.Contains(MembersOf(roots), m => m.Member.Name == nameof(WorkContractFacts.LocationApproximate));
    }

    [Fact]
    public void The_Guard_Refuses_The_Shapes_It_Exists_For()
    {
        Assert.True(CompanyArchiveRecordGuardTests.IsForbidden("CustomerName"));
        Assert.True(CompanyArchiveRecordGuardTests.IsForbidden("Street"));
        Assert.True(CompanyArchiveRecordGuardTests.IsForbidden("SpecialInstructions"));
        Assert.True(CompanyArchiveRecordGuardTests.IsForbidden("CustomerPhone"));
        Assert.False(CompanyArchiveRecordGuardTests.IsForbidden("LocationApproximate"));
        Assert.False(CompanyArchiveRecordGuardTests.IsForbidden("OrderNumber"));
    }

    [Fact]
    public void The_Facts_Round_Trip_Through_Their_Json_In_Camel_Case()
    {
        var facts = new WorkContractFacts(
            "ORD-1", new DateTime(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc), 180, 1500m, "CZK", "Praha · 120 xx", "cz", 3, 1,
            [new WorkContractFactsLine("svc-1", "General Cleaning")],
            [new WorkContractFactsLine("pkg-1", "Essential Clean")],
            ["insideOven"]);

        var json = facts.ToJson();

        Assert.Contains("\"orderNumber\":\"ORD-1\"", json);
        Assert.Contains("\"extraSlugs\":[\"insideOven\"]", json);
        Assert.DoesNotContain("OrderNumber", json);
        var restored = WorkContractFacts.FromJson(json);
        Assert.Equal(facts, restored with { Services = facts.Services, Packages = facts.Packages, ExtraSlugs = facts.ExtraSlugs });
        Assert.Equal("Praha · 120 xx", restored.LocationApproximate);
        Assert.Equal("General Cleaning", restored.Services.Single().Name);
        Assert.Equal("insideOven", restored.ExtraSlugs.Single());
    }
}
