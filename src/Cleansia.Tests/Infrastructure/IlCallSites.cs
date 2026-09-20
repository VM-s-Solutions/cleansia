using System.Reflection;
using System.Reflection.Emit;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// Every <c>call</c>/<c>callvirt</c>/<c>newobj</c>/<c>ldftn</c>/<c>ldtoken</c> operand in the compiled
/// IL of the given assemblies, resolved to the method it names — the walk <c>CustomerActionAuditImmutabilityTests</c>
/// performs, for the append-only guards that need a fact about the compiled program rather than about
/// source text: a helper, a lambda or a generic method reaches a member without the type's name ever
/// appearing on the line.
/// </summary>
internal static class IlCallSites
{
    public sealed record CallSite(MethodBase Caller, MethodBase Callee)
    {
        public override string ToString() => $"{Caller.DeclaringType?.FullName}.{Caller.Name} -> {Callee.DeclaringType?.Name}.{Callee.Name}";
    }

    public static IReadOnlyList<CallSite> Walk(params Assembly[] assemblies)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var sites = new List<CallSite>();
        foreach (var assembly in assemblies)
        {
            foreach (var type in SafeTypes(assembly))
            {
                foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
                {
                    foreach (var callee in Callees(method))
                    {
                        sites.Add(new CallSite(method, callee));
                    }
                }
            }
        }

        return sites;
    }

    /// <summary>An async method's body lives in a compiler-generated nested state machine; name the method it belongs to.</summary>
    public static string OwningMethodName(MethodBase method)
    {
        var type = method.DeclaringType!;
        if (type.IsNested && type.Name.StartsWith('<'))
        {
            var owner = type.Name[1..type.Name.IndexOf('>')];
            return $"{type.DeclaringType!.FullName}.{owner}";
        }

        return $"{type.FullName}.{method.Name}";
    }

    public static bool SameMethod(MethodBase expected, MethodBase actual) =>
        actual.MetadataToken == expected.MetadataToken && actual.Module == expected.Module;

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
