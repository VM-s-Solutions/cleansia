using System.Reflection;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Mappers;

public static class CodeMappers
{
    public static IEnumerable<Code> MapToCodeFromAssembly(this Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(type => type.IsEnum)
            .SelectMany(enumType => Enum
                .GetValues(enumType)
                .Cast<object>()
                .Select(@enum => new Code(
                    Type: enumType.Name,
                    Name: @enum.ToString()!,
                    Value: Convert.ToInt32(@enum)))
                .ToList());
    }

    public static Code MapToCode<TEnum>(this TEnum enumValue)
        where TEnum : struct, Enum
    {
        return new Code(
            Type: typeof(TEnum).Name,
            Name: enumValue.ToString()!,
            Value: Convert.ToInt32(enumValue));
    }
}