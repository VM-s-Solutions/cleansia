using System.Reflection;
using System.Text.Json.Nodes;
using Cleansia.Infra.Common.Attributes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Cleansia.Web.Customer.SwaggerSchemaFilters;

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

        var useIntegerValues = context.Type.GetCustomAttribute<SwaggerEnumAsIntAttribute>() != null;

        openApiSchema.Enum ??= [];
        openApiSchema.Enum.Clear();
        var enumNames = new JsonArray();

        foreach (var enumValue in Enum.GetValues(context.Type))
        {
            if (useIntegerValues)
            {
                openApiSchema.Enum.Add((JsonNode)(int)enumValue);
            }
            else
            {
                // String enums are more compatible with OpenAPI code generators than ints.
                openApiSchema.Enum.Add((JsonNode)enumValue.ToString()!);
            }

            enumNames.Add((JsonNode)enumValue.ToString()!);
        }

        openApiSchema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        openApiSchema.Extensions["x-enumNames"] = new JsonNodeExtension(enumNames);

        if (useIntegerValues)
        {
            openApiSchema.Type = JsonSchemaType.Integer;
            openApiSchema.Format = "int32";
        }
        else
        {
            openApiSchema.Type = JsonSchemaType.String;
            openApiSchema.Format = null;
        }
    }
}
