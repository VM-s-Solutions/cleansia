using Cleansia.Core.AppServices.Shared.DTOs.Sorting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using SortDirection = Cleansia.Core.Domain.Sorting.Common.SortDirection;

namespace Cleansia.Config.Swagger;

/// <summary>
/// States a query-bound sort as <c>Sort[0].Field</c> and <c>Sort[0].Direction</c>, the form ASP.NET
/// binds: OpenAPI 3.0 defines no query serialization for an array of objects, so a generator reading
/// <c>Sort</c> as one would send a form the server binds to an empty list.
/// → /decisions/adr-0075
/// </summary>
public sealed class IndexedSortOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters is null)
        {
            return;
        }

        foreach (var sort in context.ApiDescription.ParameterDescriptions.Where(IsQuerySort))
        {
            var position = PositionOf(operation.Parameters, sort.Name);
            if (position < 0)
            {
                continue;
            }

            operation.Parameters[position] = new OpenApiParameter
            {
                Name = $"{sort.Name}[0].{nameof(SortDefinition.Field)}",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
            };
            operation.Parameters.Insert(position + 1, new OpenApiParameter
            {
                Name = $"{sort.Name}[0].{nameof(SortDefinition.Direction)}",
                In = ParameterLocation.Query,
                Schema = context.SchemaGenerator.GenerateSchema(typeof(SortDirection), context.SchemaRepository),
            });
        }
    }

    private static bool IsQuerySort(ApiParameterDescription parameter) =>
        parameter.Source == BindingSource.Query
        && parameter.Type is not null
        && typeof(IEnumerable<SortDefinition>).IsAssignableFrom(parameter.Type);

    private static int PositionOf(IList<IOpenApiParameter> parameters, string name)
    {
        for (var position = 0; position < parameters.Count; position++)
        {
            if (parameters[position].Name == name)
            {
                return position;
            }
        }

        return -1;
    }
}
