using System.Reflection;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database.Repositories;
using Cleansia.Tests.Infrastructure;

namespace Cleansia.Tests.Features.Contracts;

/// <summary>
/// ADR-0068 D2 (Verification #1) — the acceptance row is append-only by DISCIPLINE, not by type, exactly
/// as the customer audit row is: private setters, one factory, one sanctioned mutator that blanks the
/// three request-metadata columns, a repository interface that declares reads and the two pseudonymising
/// writes and nothing else, and — walking the compiled IL of the three assemblies that can touch the
/// table — no call site that removes or deactivates a row, reads or sets <c>IsActive</c> on one, or
/// calls <c>Pseudonymise</c> from anywhere but the two repository writes. The row is built by the
/// acceptor alone.
/// </summary>
public sealed class WorkContractAcceptanceImmutabilityTests
{
    private static readonly Assembly[] Walked =
    [
        typeof(WorkContractAcceptance).Assembly,
        typeof(WorkContractAcceptor).Assembly,
        typeof(WorkContractAcceptanceRepository).Assembly
    ];

    private static readonly string[] ForbiddenRepositoryMembers = ["Remove", "RemoveRange", "Deactivate"];
    private static readonly string[] ForbiddenContextMembers = ["Remove", "RemoveRange", "Update", "UpdateRange", "Entry"];

    private static readonly Lazy<IReadOnlyList<IlCallSites.CallSite>> Sites = new(() => IlCallSites.Walk(Walked));

    [Fact]
    public void Every_Column_Has_A_Private_Setter()
    {
        var writable = typeof(WorkContractAcceptance)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(writable);
    }

    [Fact]
    public void The_Only_Public_Members_Are_The_Factory_And_Pseudonymise()
    {
        var declared = typeof(WorkContractAcceptance)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .Order()
            .ToList();

        Assert.Equal([nameof(WorkContractAcceptance.Create), nameof(WorkContractAcceptance.Pseudonymise)], declared);
        Assert.Empty(typeof(WorkContractAcceptance).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void Pseudonymise_Changes_Exactly_The_Three_Request_Metadata_Columns()
    {
        var acceptance = WorkContractAcceptance.Create(
            "01ORDER000000000000000001", "01SEAT0000000000000000001", "01EMP00000000000000000001",
            TestUtilities.WorkContractTestData.Document().TextFor("en")!, "2026-01-01", "cleansia.partner",
            "203.0.113.9", "Firefox", "device-1", "{}");
        var before = Snapshot(acceptance);

        acceptance.Pseudonymise();

        var changed = Snapshot(acceptance).Where(kv => !Equals(kv.Value, before[kv.Key])).Select(kv => kv.Key).Order().ToList();
        Assert.Equal(
            [nameof(WorkContractAcceptance.DeviceId), nameof(WorkContractAcceptance.DeviceLabel), nameof(WorkContractAcceptance.IpAddress)],
            changed);
    }

    [Fact]
    public void The_Repository_Interface_Declares_The_Reads_And_The_Two_Pseudonymising_Writes_Only()
    {
        var declared = typeof(IWorkContractAcceptanceRepository)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Order()
            .ToList();

        Assert.Equal(
            [
                nameof(IWorkContractAcceptanceRepository.AnyForSeatAsync),
                nameof(IWorkContractAcceptanceRepository.GetByEmployeeIdNoTrackingAsync),
                nameof(IWorkContractAcceptanceRepository.GetByIdIgnoringTenantAsync),
                nameof(IWorkContractAcceptanceRepository.GetForOrdersAsync),
                nameof(IWorkContractAcceptanceRepository.GetForSeatsAsync),
                nameof(IWorkContractAcceptanceRepository.PseudonymiseExpiredAsync),
                nameof(IWorkContractAcceptanceRepository.PseudonymiseForEmployeeAsync),
            ],
            declared);
    }

    [Fact]
    public void No_Call_Site_Removes_Or_Deactivates_An_Acceptance_Row()
    {
        var offenders = Sites.Value
            .Where(site => RemovesOrDeactivatesThroughTheRepository(site) || ReachesTheRowThroughTheContext(site))
            .Select(site => site.ToString())
            .Distinct()
            .Order()
            .ToList();

        Assert.True(offenders.Count == 0,
            "ADR-0068 D2: the acceptance row is append-only. These call sites remove or deactivate it:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void No_Method_That_Handles_An_Acceptance_Row_Reads_Or_Sets_IsActive()
    {
        var isActive = typeof(BaseEntity).GetProperty(nameof(BaseEntity.IsActive))!;
        var accessors = new[] { isActive.GetMethod!, isActive.SetMethod! };

        var handlers = Sites.Value
            .GroupBy(site => site.Caller)
            .Where(group => References(group.Key, typeof(WorkContractAcceptance)))
            .ToList();

        Assert.NotEmpty(handlers);

        var offenders = handlers
            .Where(group => group.Any(site => accessors.Any(accessor => IlCallSites.SameMethod(accessor, site.Callee))))
            .Select(group => $"{group.Key.DeclaringType?.FullName}.{group.Key.Name}")
            .Distinct()
            .Order()
            .ToList();

        Assert.True(offenders.Count == 0,
            "ADR-0068 D2: nothing reads or sets IsActive on an acceptance row. These methods handle the row and touch IsActive:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Pseudonymise_Is_Called_By_The_Two_Repository_Writes_And_Nowhere_Else()
    {
        var pseudonymise = typeof(WorkContractAcceptance).GetMethod(nameof(WorkContractAcceptance.Pseudonymise))!;

        var callers = Sites.Value
            .Where(site => IlCallSites.SameMethod(pseudonymise, site.Callee))
            .Select(site => IlCallSites.OwningMethodName(site.Caller))
            .Distinct()
            .Order()
            .ToList();

        Assert.Equal(
            [
                $"{typeof(WorkContractAcceptanceRepository).FullName}.{nameof(WorkContractAcceptanceRepository.PseudonymiseExpiredAsync)}",
                $"{typeof(WorkContractAcceptanceRepository).FullName}.{nameof(WorkContractAcceptanceRepository.PseudonymiseForEmployeeAsync)}"
            ],
            callers);
    }

    [Fact]
    public void The_Row_Is_Built_By_The_Acceptor_Alone()
    {
        var create = typeof(WorkContractAcceptance).GetMethod(nameof(WorkContractAcceptance.Create))!;

        var callers = Sites.Value
            .Where(site => IlCallSites.SameMethod(create, site.Callee))
            .Select(site => IlCallSites.OwningMethodName(site.Caller))
            .Distinct()
            .ToList();

        Assert.Equal([$"{typeof(WorkContractAcceptor).FullName}.{nameof(WorkContractAcceptor.StageAsync)}"], callers);
    }

    private static Dictionary<string, object?> Snapshot(WorkContractAcceptance acceptance) =>
        typeof(WorkContractAcceptance)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p.GetValue(acceptance));

    private static bool RemovesOrDeactivatesThroughTheRepository(IlCallSites.CallSite site) =>
        ForbiddenRepositoryMembers.Contains(site.Callee.Name)
        && TargetsTheTable(site.Callee.DeclaringType);

    private static bool ReachesTheRowThroughTheContext(IlCallSites.CallSite site)
    {
        if (!ForbiddenContextMembers.Contains(site.Callee.Name)
            || site.Callee.DeclaringType is not { } declaringType
            || !typeof(Microsoft.EntityFrameworkCore.DbContext).IsAssignableFrom(declaringType))
        {
            return false;
        }

        return site.Callee.IsGenericMethod
            ? site.Callee.GetGenericArguments().Contains(typeof(WorkContractAcceptance))
            : References(site.Caller, typeof(WorkContractAcceptance));
    }

    private static bool TargetsTheTable(Type? declaringType)
    {
        if (declaringType is null)
        {
            return false;
        }

        if (declaringType == typeof(IWorkContractAcceptanceRepository) || declaringType == typeof(WorkContractAcceptanceRepository))
        {
            return true;
        }

        return declaringType.IsGenericType
               && declaringType.GetGenericArguments().Contains(typeof(WorkContractAcceptance));
    }

    private static bool References(MethodBase method, Type entity)
    {
        if (method.DeclaringType is { } declaring && (References(declaring, entity) || DeclaresFieldOf(declaring, entity)))
        {
            return true;
        }

        if (method.GetParameters().Any(p => References(p.ParameterType, entity)))
        {
            return true;
        }

        if (method is MethodInfo info && References(info.ReturnType, entity))
        {
            return true;
        }

        var body = method.GetMethodBody();
        return body is not null && body.LocalVariables.Any(v => References(v.LocalType, entity));
    }

    private static bool DeclaresFieldOf(Type type, Type entity) =>
        type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(f => References(f.FieldType, entity));

    private static bool References(Type type, Type entity) => References(type, entity, []);

    private static bool References(Type type, Type entity, HashSet<Type> visited)
    {
        if (type == entity)
        {
            return true;
        }

        if (type.IsGenericParameter || !visited.Add(type))
        {
            return false;
        }

        if (type.IsGenericType && type.GetGenericArguments().Any(arg => References(arg, entity, visited)))
        {
            return true;
        }

        if (type.HasElementType && References(type.GetElementType()!, entity, visited))
        {
            return true;
        }

        if (type.BaseType is { } baseType && baseType != typeof(object) && References(baseType, entity, visited))
        {
            return true;
        }

        return type.IsNested && type.DeclaringType is not null && References(type.DeclaringType, entity, visited);
    }
}
