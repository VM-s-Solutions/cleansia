using System.Reflection;
using System.Reflection.Emit;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D5 (Verification #4) — the customer audit row is append-only by DISCIPLINE, not by type:
/// <c>IRepository&lt;T&gt;</c> hands every repository <c>Remove</c>/<c>RemoveRange</c>/<c>Deactivate</c>/
/// <c>DeactivateRange</c>, and <c>BaseEntity.IsActive</c> is a public setter. So this walks the compiled
/// IL of the three assemblies that can touch the table and asserts that no method body invokes one of
/// those four on the customer repository or the entity's <c>DbSet</c>, nor reaches the row through the
/// context itself (<c>DbContext.Remove&lt;T&gt;</c> is a generic METHOD on a non-generic type, and
/// <c>RemoveRange(params object[])</c> / <c>Entry(object)</c> carry no type at all — the latter are
/// flagged when the calling method handles a <c>CustomerActionAudit</c>), that no method body which
/// handles a <c>CustomerActionAudit</c> reads or sets <c>IsActive</c>, and that the one sanctioned
/// mutator, <see cref="CustomerActionAudit.Pseudonymise"/>, is called from the erasure walk and nowhere
/// else.
///
/// <para>IL rather than source text because a call site is a fact about the compiled program: a helper,
/// a lambda or a generic method reaches the same member without the type's name ever appearing on the
/// line. The walk resolves every <c>call</c>/<c>callvirt</c>/<c>newobj</c>/<c>ldftn</c>/<c>ldtoken</c>
/// operand through the declaring module, so a member reached through a generic instantiation over
/// <c>CustomerActionAudit</c> — a type's or a method's — resolves to that instantiation.</para>
/// </summary>
public sealed class CustomerActionAuditImmutabilityTests
{
    private static readonly Assembly[] Walked =
    [
        typeof(CustomerActionAudit).Assembly,
        typeof(Core.AppServices.Auditing.AuditGate).Assembly,
        typeof(CustomerActionAuditRepository).Assembly
    ];

    private static readonly string[] ForbiddenRepositoryMembers =
        ["Remove", "RemoveRange", "Deactivate", "DeactivateRange"];

    private static readonly string[] ForbiddenContextMembers =
        ["Remove", "RemoveRange", "Update", "UpdateRange", "Entry"];

    private sealed record CallSite(MethodBase Caller, MethodBase Callee)
    {
        public override string ToString() => $"{Caller.DeclaringType?.FullName}.{Caller.Name} -> {Callee.DeclaringType?.Name}.{Callee.Name}";
    }

    private static readonly Lazy<IReadOnlyList<CallSite>> Sites = new(() => AllCallSites().ToList());

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
            .Where(group => group.Any(site => accessors.Any(accessor => SameMethod(accessor, site.Callee))))
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
            .Where(site => SameMethod(pseudonymise, site.Callee))
            .Select(site => OwningMethodName(site.Caller))
            .Distinct()
            .Order()
            .ToList();

        // Anti-vacuity: the walk must actually see the sanctioned caller, or every "no offender" above
        // would be a walk that resolves nothing.
        Assert.Equal(
            [$"{typeof(CustomerActionAuditRepository).FullName}.{nameof(CustomerActionAuditRepository.PseudonymiseForSubjectAsync)}"],
            callers);
    }

    [Fact]
    public void The_Repository_Interface_Adds_Only_The_Erasure_Write_And_The_Retention_Delete()
    {
        var declared = typeof(ICustomerActionAuditRepository)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Order()
            .ToList();

        Assert.Equal(
            [nameof(ICustomerActionAuditRepository.DeleteExpiredAsync), nameof(ICustomerActionAuditRepository.PseudonymiseForSubjectAsync)],
            declared);
    }

    /// <summary>An async method's body lives in a compiler-generated nested state machine; name the method it belongs to.</summary>
    private static string OwningMethodName(MethodBase method)
    {
        var type = method.DeclaringType!;
        if (type.IsNested && type.Name.StartsWith('<'))
        {
            var owner = type.Name[1..type.Name.IndexOf('>')];
            return $"{type.DeclaringType!.FullName}.{owner}";
        }

        return $"{type.FullName}.{method.Name}";
    }

    private static bool RemovesOrDeactivatesThroughTheRepository(CallSite site) =>
        ForbiddenRepositoryMembers.Contains(site.Callee.Name)
        && TargetsTheCustomerTable(site.Callee.DeclaringType);

    private static bool ReachesTheRowThroughTheContext(CallSite site)
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

    private static bool SameMethod(MethodBase expected, MethodBase actual) =>
        actual.MetadataToken == expected.MetadataToken && actual.Module == expected.Module;

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

    private static IEnumerable<CallSite> AllCallSites()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var assembly in Walked)
        {
            foreach (var type in SafeTypes(assembly))
            {
                foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
                {
                    foreach (var callee in Callees(method))
                    {
                        yield return new CallSite(method, callee);
                    }
                }
            }
        }
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static IEnumerable<MethodBase> Callees(MethodBase method)
    {
        byte[]? il;
        try
        {
            il = method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (Exception)
        {
            yield break;
        }

        if (il is null)
        {
            yield break;
        }

        var typeArguments = method.DeclaringType is { IsGenericType: true } declaring ? declaring.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;

        var position = 0;
        while (position < il.Length)
        {
            if (!TryReadOpCode(il, ref position, out var opCode))
            {
                yield break;
            }

            var operandStart = position;
            position += OperandSize(opCode, il, position);

            if (opCode.OperandType is not (OperandType.InlineMethod or OperandType.InlineTok))
            {
                continue;
            }

            var token = BitConverter.ToInt32(il, operandStart);
            MethodBase? callee;
            try
            {
                callee = opCode.OperandType == OperandType.InlineMethod
                    ? method.Module.ResolveMethod(token, typeArguments, methodArguments)
                    : method.Module.ResolveMember(token, typeArguments, methodArguments) as MethodBase;
            }
            catch (Exception)
            {
                callee = null;
            }

            if (callee is not null)
            {
                yield return callee;
            }
        }
    }

    private static readonly Dictionary<short, OpCode> OpCodeTable = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .GroupBy(op => op.Value)
        .ToDictionary(g => g.Key, g => g.First());

    private static bool TryReadOpCode(byte[] il, ref int position, out OpCode opCode)
    {
        short value = il[position++];
        if (value == 0xFE && position < il.Length)
        {
            value = (short)(0xFE00 | il[position++]);
        }

        return OpCodeTable.TryGetValue(value, out opCode);
    }

    private static int OperandSize(OpCode opCode, byte[] il, int position) =>
        opCode.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, position),
            _ => 4
        };
}
