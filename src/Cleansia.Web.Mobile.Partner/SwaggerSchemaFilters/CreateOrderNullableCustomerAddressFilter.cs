using Cleansia.Core.AppServices.Features.Orders;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Cleansia.Web.Mobile.Partner.SwaggerSchemaFilters;

// See CreateOrderNullableCustomerAddressFilter on the Customer API for the full rationale.
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

        var wrapped = new OpenApiSchema
        {
            Type = JsonSchemaType.Object | JsonSchemaType.Null,
            AllOf = [property],
        };

        openApiSchema.Properties[propertyName] = wrapped;
    }
}
