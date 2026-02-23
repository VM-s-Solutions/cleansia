using System.Reflection;
using Cleansia.Infra.Common.Attributes;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Cleansia.Web.Admin.SwaggerSchemaFilters;

public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum)
        {
            return;
        }

        // Check if this enum should be serialized as integers
        var useIntegerValues = context.Type.GetCustomAttribute<SwaggerEnumAsIntAttribute>() != null;

        schema.Enum.Clear();
        var enumNames = new OpenApiArray();

        // Loop through enum values
        foreach (var enumValue in Enum.GetValues(context.Type))
        {
            if (useIntegerValues)
            {
                // Add the numeric value to the schema enum
                schema.Enum.Add(new OpenApiInteger((int)enumValue));
            }
            else
            {
                // Add the string name to the schema enum (more compatible with code generators)
                schema.Enum.Add(new OpenApiString(enumValue.ToString()));
            }

            // Add the enum name to x-enumNames
            enumNames.Add(new OpenApiString(enumValue.ToString()));
        }

        // Set x-enumNames to contain names of the enum values
        schema.Extensions["x-enumNames"] = enumNames;

        if (useIntegerValues)
        {
            // Set type to integer for numeric enums
            schema.Type = "integer";
            schema.Format = "int32";
        }
        else
        {
            // Set type to string for named enums
            schema.Type = "string";
            schema.Format = null;
        }
    }
}