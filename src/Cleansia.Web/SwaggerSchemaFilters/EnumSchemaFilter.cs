using System.Reflection;
using System.Text.Json.Nodes;
using Cleansia.Infra.Common.Attributes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Cleansia.Web.SwaggerSchemaFilters;

public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum)
        {
            return;
        }

        if (schema is not OpenApiSchema openApiSchema)
        {
            return;
        }

        // Check if this enum should be serialized as integers
        var useIntegerValues = context.Type.GetCustomAttribute<SwaggerEnumAsIntAttribute>() != null;

        openApiSchema.Enum ??= [];
        openApiSchema.Enum.Clear();
        var enumNames = new JsonArray();

        // Loop through enum values
        foreach (var enumValue in Enum.GetValues(context.Type))
        {
            if (useIntegerValues)
            {
                // Add the numeric value to the schema enum
                openApiSchema.Enum.Add((JsonNode)(int)enumValue);
            }
            else
            {
                // Add the string name to the schema enum (more compatible with code generators)
                openApiSchema.Enum.Add((JsonNode)enumValue.ToString()!);
            }

            // Add the enum name to x-enumNames
            enumNames.Add((JsonNode)enumValue.ToString()!);
        }

        // Set x-enumNames to contain names of the enum values
        openApiSchema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        openApiSchema.Extensions["x-enumNames"] = new JsonNodeExtension(enumNames);

        if (useIntegerValues)
        {
            // Set type to integer for numeric enums
            openApiSchema.Type = JsonSchemaType.Integer;
            openApiSchema.Format = "int32";
        }
        else
        {
            // Set type to string for named enums
            openApiSchema.Type = JsonSchemaType.String;
            openApiSchema.Format = null;
        }
    }
}
