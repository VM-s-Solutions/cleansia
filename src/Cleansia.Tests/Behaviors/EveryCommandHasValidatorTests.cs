using System.Reflection;
using Cleansia.Core.AppServices.Abstractions;
using FluentValidation;
using AssemblyReference = Cleansia.Core.AppServices.AssemblyReference;

namespace Cleansia.Tests.Behaviors;

/// <summary>
/// Guard against the validator-less-command class of bug. ValidationPipelineBehavior fails any request
/// whose type is named "Command" (or whose declaring type ends in "Command") when no IValidator is
/// registered for it — so a command that ships without a validator throws at runtime on its first call.
/// The customer GDPR DeleteUserAccount.Command shipped exactly that way and broke the Article-17
/// "delete my account" endpoint. This test enumerates every command type in the AppServices assembly
/// (the same set AddValidatorsFromAssembly scans) and asserts each has a concrete AbstractValidator,
/// so the gap can never recur silently.
/// </summary>
public class EveryCommandHasValidatorTests
{
    [Fact]
    public void Every_Command_Type_Has_A_Concrete_Validator()
    {
        var assembly = AssemblyReference.Assembly;
        var allTypes = assembly.GetTypes();

        // Mirror ValidationPipelineBehavior's EXACT detection (it is name-based, not interface-based):
        // a validator is required only when the request type is named "Command" or its declaring type
        // ends in "Command". A read named "Query" (even one that implements ICommand<T>) is NOT gated.
        var commandTypes = allTypes
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(IsCommand)
            .Where(IsValidatorRequiredByPipeline)
            .ToList();

        Assert.NotEmpty(commandTypes); // sanity: the scan actually found commands

        // Concrete AbstractValidator<T> subclasses present in the assembly, keyed by the validated type.
        var validatedTypes = allTypes
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Select(GetValidatedType)
            .Where(v => v is not null)
            .Select(v => v!)
            .ToHashSet();

        var missing = commandTypes
            .Where(c => !validatedTypes.Contains(c))
            .Select(c => c.FullName)
            .OrderBy(n => n)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Every command must have a FluentValidation validator (even an empty one) or the validation "
            + "pipeline throws at runtime on first use. Missing validators for: "
            + string.Join(", ", missing));
    }

    private static bool IsCommand(Type t) =>
        typeof(ICommand).IsAssignableFrom(t)
        || t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));

    // The exact condition ValidationPipelineBehavior uses to demand a validator.
    private static bool IsValidatorRequiredByPipeline(Type t) =>
        t.Name == "Command" || t.DeclaringType?.Name.EndsWith("Command") == true;

    private static Type? GetValidatedType(Type t)
    {
        for (var b = t.BaseType; b is not null; b = b.BaseType)
        {
            if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            {
                return b.GetGenericArguments()[0];
            }
        }
        return null;
    }
}
