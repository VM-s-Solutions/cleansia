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
        // Check if the type is an enum and has the SwaggerEnumAsIntAttribute
        if (!context.Type.IsEnum || context.Type.GetCustomAttribute<SwaggerEnumAsIntAttribute>() == null)
        {
            return;
        }
        schema.Enum.Clear();
        var enumNames = new OpenApiArray();

        // Loop through enum values to add both numeric values and names
        foreach (var enumValue in Enum.GetValues(context.Type))
        {
            // Add the numeric value to the schema enum
            schema.Enum.Add(new OpenApiInteger((int)enumValue));

            // Add the enum name to x-enumNames
            enumNames.Add(new OpenApiString(enumValue.ToString()));
        }

        // Set x-enumNames to contain names of the enum values
        schema.Extensions["x-enumNames"] = enumNames;

        // Set type to integer to reflect numeric enums in Swagger
        schema.Type = "integer";
        schema.Format = "int32";
    }
}