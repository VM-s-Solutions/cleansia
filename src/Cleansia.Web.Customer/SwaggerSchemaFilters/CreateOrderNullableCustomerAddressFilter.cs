using Cleansia.Core.AppServices.Features.Orders;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Cleansia.Web.Customer.SwaggerSchemaFilters;

// Targeted fix: CreateOrder.Command.CustomerAddress is nullable in C# (XOR with SavedAddressId).
// Without the global SupportNonNullableReferenceTypes flag, Swashbuckle emits the property as
// a bare $ref which NSwag types as non-optional `customerAddress!: AddressDto` regardless of
// the required-array membership. Turning the global flag on would ripple ~240 properties.
//
// Workaround: replace the property schema with `{ type: "null" | "object", allOf: [{ $ref }] }`
// which the OpenAPI 3.0 serializer writes as `nullable: true` + allOf-wrapped ref, and NSwag
// generates as `customerAddress: AddressDto | undefined`.
public class CreateOrderNullableCustomerAddressFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(CreateOrder.Command))
        {
            return;
        }

        if (schema is not OpenApiSchema openApiSchema)
        {
            return;
        }

        if (openApiSchema.Properties == null)
        {
            return;
        }

        const string propertyName = "customerAddress";
        if (!openApiSchema.Properties.TryGetValue(propertyName, out var property))
        {
            return;
        }

        // Wrap the existing $ref inside an allOf so we can attach the Null type flag alongside.
        var wrapped = new OpenApiSchema
        {
            Type = JsonSchemaType.Object | JsonSchemaType.Null,
            AllOf = [property],
        };

        openApiSchema.Properties[propertyName] = wrapped;
    }
}
