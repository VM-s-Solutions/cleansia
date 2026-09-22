using System.Reflection;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database.Repositories;
using Cleansia.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D5 (Verification #4) — the customer audit row is append-only by DISCIPLINE, not by type:
/// <c>IRepository&lt;T&gt;</c> hands every repository <c>Remove</c>/<c>RemoveRange</c>/<c>Deactivate</c>,
/// and <c>BaseEntity.IsActive</c> is a public setter. So this walks the compiled
/// IL of the three assemblies that can touch the table and asserts that no method body invokes one of
/// those three on the customer repository or the entity's <c>DbSet</c>, nor reaches the row through the
/// context itself (<c>DbContext.Remove&lt;T&gt;</c> is a generic METHOD on a non-generic type, and
/// <c>RemoveRange(params object[])</c> / <c>Entry(object)</c> carry no type at all — the latter are
/// flagged when the calling method handles a <c>CustomerActionAudit</c>), that no method body which
/// handles a <c>CustomerActionAudit</c> reads or sets <c>IsActive</c>, and that the one sanctioned
/// mutator, <see cref="CustomerActionAudit.Pseudonymise"/>, is called from the erasure walk's two
/// repository writes — the subject's own rows and the guest rows on the subject's orders — and nowhere
/// else.
///
/// <para>IL rather than source text because a call site is a fact about the compiled program: a helper,
/// a lambda or a generic method reaches the same member without the type's name ever appearing on the
/// line. The walk (<see cref="IlCallSites"/>) resolves every <c>call</c>/<c>callvirt</c>/<c>newobj</c>/
/// <c>ldftn</c>/<c>ldtoken</c> operand through the declaring module, so a member reached through a
/// generic instantiation over <c>CustomerActionAudit</c> — a type's or a method's — resolves to that
/// instantiation.</para>
/// </summary>
public sealed class CustomerActionAuditImmutabilityTests
{
    private static readonly Assembly[] Walked =
    [
        typeof(CustomerActionAudit).Assembly,
        typeof(Core.AppServices.Auditing.AuditGate).Assembly,
        typeof(CustomerActionAuditRepository).Assembly
    ];

    private static readonly string[] ForbiddenRepositoryMembers = ["Remove", "RemoveRange", "Deactivate"];

    private static readonly string[] ForbiddenContextMembers =
        ["Remove", "RemoveRange", "Update", "UpdateRange", "Entry"];

    private static readonly Lazy<IReadOnlyList<IlCallSites.CallSite>> Sites = new(() => IlCallSites.Walk(Walked));

    [Fact]
    public void No_Call_Site_Removes_Or_Deactivates_A_Customer_Audit_Row()
    {
        var offenders = Sites.Value
            .Where(site => RemovesOrDeactivatesThroughTheRepository(site) || ReachesTheRowThroughTheContext(site))
            .Select(site => site.ToString())
            .Distinct()
            .Order()
            .ToList();

        Assert.True(offenders.Count == 0,
            "ADR-0062 D5: the customer audit row is append-only. These call sites remove or deactivate it:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void No_Method_That_Handles_A_Customer_Audit_Row_Reads_Or_Sets_IsActive()
    {
        var isActive = typeof(BaseEntity).GetProperty(nameof(BaseEntity.IsActive))!;
        var accessors = new[] { isActive.GetMethod!, isActive.SetMethod! };

        var handlers = Sites.Value
            .GroupBy(site => site.Caller)
            .Where(group => References(group.Key, typeof(CustomerActionAudit)))
            .ToList();

        // Anti-vacuity: the factory builds the row and the writer takes it, so the walk must see handlers.
        Assert.NotEmpty(handlers);

        var offenders = handlers
            .Where(group => group.Any(site => accessors.Any(accessor => IlCallSites.SameMethod(accessor, site.Callee))))
            .Select(group => $"{group.Key.DeclaringType?.FullName}.{group.Key.Name}")
            .Distinct()
            .Order()
            .ToList();

        Assert.True(offenders.Count == 0,
            "ADR-0062 D5: nothing reads or sets IsActive on a customer audit row — a soft delete is a delete. "
            + "These methods handle the row and touch IsActive:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Pseudonymise_Is_Called_By_The_Erasure_Walk_And_Nowhere_Else()
    {
        var pseudonymise = typeof(CustomerActionAudit).GetMethod(nameof(CustomerActionAudit.Pseudonymise))!;

        var callers = Sites.Value
            .Where(site => IlCallSites.SameMethod(pseudonymise, site.Callee))
            .Select(site => IlCallSites.OwningMethodName(site.Caller))
            .Distinct()
            .Order()
            .ToList();

        // Anti-vacuity: the walk must actually see the sanctioned callers, or every "no offender" above
        // would be a walk that resolves nothing.
        Assert.Equal(
            [
                $"{typeof(CustomerActionAuditRepository).FullName}.{nameof(CustomerActionAuditRepository.PseudonymiseForSubjectAsync)}",
                $"{typeof(CustomerActionAuditRepository).FullName}.{nameof(CustomerActionAuditRepository.PseudonymiseGuestRowsForOrdersAsync)}"
            ],
            callers);
    }

    [Fact]
    public void The_Repository_Interface_Adds_Only_The_Two_Erasure_Writes_The_Retention_Delete_And_The_By_User_Reads()
    {
        var declared = typeof(ICustomerActionAuditRepository)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Order()
            .ToList();

        Assert.Equal(
            [
                nameof(ICustomerActionAuditRepository.DeleteExpiredAsync),
                nameof(ICustomerActionAuditRepository.GetCountForUserAsync),
                nameof(ICustomerActionAuditRepository.GetPagedSortForUser),
                nameof(ICustomerActionAuditRepository.GetQueryableForUser),
                nameof(ICustomerActionAuditRepository.PseudonymiseForSubjectAsync),
                nameof(ICustomerActionAuditRepository.PseudonymiseGuestRowsForOrdersAsync)
            ],
            declared);
    }

    private static bool RemovesOrDeactivatesThroughTheRepository(IlCallSites.CallSite site) =>
        ForbiddenRepositoryMembers.Contains(site.Callee.Name)
        && TargetsTheCustomerTable(site.Callee.DeclaringType);

    private static bool ReachesTheRowThroughTheContext(IlCallSites.CallSite site)
    {
        if (!ForbiddenContextMembers.Contains(site.Callee.Name)
            || site.Callee.DeclaringType is not { } declaringType
            || !typeof(DbContext).IsAssignableFrom(declaringType))
        {
            return false;
        }

        return site.Callee.IsGenericMethod
            ? site.Callee.GetGenericArguments().Contains(typeof(CustomerActionAudit))
            : References(site.Caller, typeof(CustomerActionAudit));
    }

    private static bool TargetsTheCustomerTable(Type? declaringType)
    {
        if (declaringType is null)
        {
            return false;
        }

        if (declaringType == typeof(ICustomerActionAuditRepository) || declaringType == typeof(CustomerActionAuditRepository))
        {
            return true;
        }

        return declaringType.IsGenericType
               && declaringType.GetGenericArguments().Contains(typeof(CustomerActionAudit));
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

    /// <summary>A state machine or closure hoists its locals into fields; a Debug build hoists more than Release.</summary>
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

        // A generic parameter's DeclaringType is the open type that declares it, which would loop.
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

        // A compiler-generated closure or state machine nested in a type that handles the row.
        return type.IsNested && type.DeclaringType is not null && References(type.DeclaringType, entity, visited);
    }
}
