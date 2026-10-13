using System.Globalization;
using System.Text.Json;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.HostTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using SortDefinition = Cleansia.Core.AppServices.Shared.DTOs.Sorting.SortDefinition;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The two mobile specs are what both native generators read, and OpenAPI 3.0 defines no query
/// serialization for an array of objects: each generator sent its own fallback for
/// <see cref="DataRangeRequest.Sort"/> and the server bound an empty list. The mobile hosts state the
/// sort as the indexed scalars ASP.NET binds (ADR-0075), and these tests keep that true.
/// </summary>
public sealed class MobileSpecSortParameterTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    public enum MobileApi
    {
        Partner,
        Customer,
    }

    private const string SortFieldName = "Sort[0].Field";
    private const string SortDirectionName = "Sort[0].Direction";
    private const string SortDirectionRef = "#/components/schemas/SortDirection";

    [Theory]
    [InlineData(MobileApi.Partner)]
    [InlineData(MobileApi.Customer)]
    public void No_query_parameter_in_a_mobile_spec_is_a_collection_of_objects(MobileApi api)
    {
        var spec = WithHost(api, SpecOf);
        var schemas = spec.GetProperty("components").GetProperty("schemas");
        var queryParameters = QueryParametersOf(spec).ToList();

        Assert.True(queryParameters.Count > 0, $"No query parameters in the {api} mobile spec; the check would be vacuous.");

        var offenders = queryParameters
            .Where(query => IsCollectionOfObjects(query.Parameter.GetProperty("schema"), schemas))
            .Select(query => $"{query.Operation} {query.Parameter.GetProperty("name").GetString()}")
            .ToList();

        Assert.True(offenders.Count == 0,
            $"The {api} mobile spec describes a query collection of objects, which OpenAPI 3.0 cannot serialize, so " +
            "each native generator sends its own fallback and the server binds nothing. State it as indexed scalars " +
            "(IndexedSortOperationFilter does this for DataRangeRequest.Sort). Offenders:\n  " + string.Join("\n  ", offenders));
    }

    [Theory]
    [InlineData(MobileApi.Partner)]
    [InlineData(MobileApi.Customer)]
    public void Every_query_sort_on_a_mobile_host_is_stated_as_the_field_and_direction_at_index_zero(MobileApi api)
    {
        var (spec, sortedOperations) = WithHost(api, services => (SpecOf(services), QuerySortOperationsOf(services)));

        Assert.True(sortedOperations.Count > 0, $"No {api} mobile operation binds a query sort; the check would be vacuous.");

        foreach (var (method, path) in sortedOperations)
        {
            var parameters = OperationParameters(spec, method, path)
                .ToDictionary(parameter => parameter.GetProperty("name").GetString()!);
            var operation = $"{method.ToUpperInvariant()} {path}";

            Assert.False(parameters.ContainsKey(nameof(DataRangeRequest.Sort)),
                $"{operation} still describes the sort as one '{nameof(DataRangeRequest.Sort)}' parameter.");

            Assert.True(parameters.TryGetValue(SortFieldName, out var field), $"{operation} has no '{SortFieldName}' parameter.");
            Assert.Equal("query", field.GetProperty("in").GetString());
            Assert.Equal("string", field.GetProperty("schema").GetProperty("type").GetString());

            Assert.True(parameters.TryGetValue(SortDirectionName, out var direction), $"{operation} has no '{SortDirectionName}' parameter.");
            Assert.Equal("query", direction.GetProperty("in").GetString());
            Assert.Equal(SortDirectionRef, direction.GetProperty("schema").GetProperty("$ref").GetString());
        }
    }

    [Fact]
    public async Task The_stated_sort_parameters_order_the_partner_order_page()
    {
        const string cleanerEmail = "sort-roundtrip-cleaner@hosttests.local";
        const string customerEmail = "sort-roundtrip-customer@hosttests.local";
        var employeeId = "";
        var idsByCleaningDate = new List<string>();
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var customer = DomainSeed.Customer(customerEmail, tenantId: HostTestTenants.Default);
            ctx.Users.Add(customer);
            var cleanerUser = DomainSeed.EmployeeUser(cleanerEmail, tenantId: HostTestTenants.Default);
            ctx.Users.Add(cleanerUser);
            var cleaner = DomainSeed.ApprovedEmployee(cleanerUser, tenantId: HostTestTenants.Default);
            ctx.Employees.Add(cleaner);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(cleaner.Id, tenantId: HostTestTenants.Default));

            // The default page is newest booked first, and the later a job is booked here the later it is
            // cleaned, so the default lists them in the reverse of the order the sort asks for.
            var bookedFrom = DateTimeOffset.UtcNow.AddDays(-1);
            for (var day = 1; day <= 3; day++)
            {
                var order = DomainSeed.NewOrder(customer.Id, customerEmail, tenantId: HostTestTenants.Default,
                    cleaningDateTime: DateTime.UtcNow.Date.AddDays(day).AddHours(9));
                order.Created("seed", bookedFrom.AddMinutes(day));
                DomainSeed.ConfirmAndAssign(order, cleaner);
                ctx.Orders.Add(order);
                idsByCleaningDate.Add(order.Id);
            }

            employeeId = cleaner.Id;
        });

        var client = MobileClient(TestJwtFactory.Mint(MobileAudience, "u-sort-roundtrip", cleanerEmail,
            UserProfile.Employee, employeeId: employeeId));
        var mine = $"/api/Order/GetPaged?Filter.EmployeeId={employeeId}&Limit=10";

        var unsorted = await PageIdsAsync(client, mine);
        Assert.Equal(Enumerable.Reverse(idsByCleaningDate), unsorted);

        var sortParameters = OperationParameters(SpecOf(MobileHost.Services), "get", "/api/Order/GetPaged")
            .Where(parameter => parameter.GetProperty("name").GetString()!.StartsWith(nameof(DataRangeRequest.Sort), StringComparison.Ordinal))
            .ToList();
        var fieldName = Assert.Single(sortParameters, parameter =>
            parameter.GetProperty("schema").TryGetProperty("type", out var type) && type.GetString() == "string")
            .GetProperty("name").GetString()!;
        var directionName = Assert.Single(sortParameters, parameter =>
            parameter.GetProperty("schema").TryGetProperty("$ref", out var reference) && reference.GetString() == SortDirectionRef)
            .GetProperty("name").GetString()!;

        var sorted = await PageIdsAsync(client,
            $"{mine}&{Uri.EscapeDataString(fieldName)}=cleaningDateTime" +
            $"&{Uri.EscapeDataString(directionName)}={(int)SortDirection.Ascending}");
        Assert.Equal(idsByCleaningDate, sorted);
    }

    private T WithHost<T>(MobileApi api, Func<IServiceProvider, T> read)
    {
        if (api == MobileApi.Partner)
        {
            return read(MobileHost.Services);
        }

        using var customerHost = new HostTestApplicationFactory<Cleansia.Web.Mobile.Customer.Program>(Db.ConnectionString);
        return read(customerHost.Services);
    }

    private static JsonElement SpecOf(IServiceProvider services)
    {
        var document = services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        document.SerializeAsV3(new OpenApiJsonWriter(text));
        using var json = JsonDocument.Parse(text.ToString());
        return json.RootElement.Clone();
    }

    private static List<(string Method, string Path)> QuerySortOperationsOf(IServiceProvider services) =>
        services.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Where(api => api.ParameterDescriptions.Any(parameter =>
                parameter.Source == BindingSource.Query
                && parameter.Type is not null
                && typeof(IEnumerable<SortDefinition>).IsAssignableFrom(parameter.Type)))
            .Select(api => (api.HttpMethod!.ToLowerInvariant(), "/" + api.RelativePath))
            .ToList();

    private static IEnumerable<(string Operation, JsonElement Parameter)> QueryParametersOf(JsonElement spec) =>
        from path in spec.GetProperty("paths").EnumerateObject()
        from operation in path.Value.EnumerateObject()
        where operation.Value.ValueKind == JsonValueKind.Object && operation.Value.TryGetProperty("parameters", out _)
        from parameter in operation.Value.GetProperty("parameters").EnumerateArray()
        where parameter.GetProperty("in").GetString() == "query"
        select ($"{operation.Name.ToUpperInvariant()} {path.Name}", parameter);

    private static List<JsonElement> OperationParameters(JsonElement spec, string method, string path) =>
        spec.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("parameters").EnumerateArray().ToList();

    private static bool IsCollectionOfObjects(JsonElement schema, JsonElement schemas)
    {
        if (!schema.TryGetProperty("type", out var type) || type.GetString() != "array")
        {
            return false;
        }

        var items = schema.GetProperty("items");
        if (items.TryGetProperty("$ref", out var reference))
        {
            items = schemas.GetProperty(reference.GetString()!.Split('/')[^1]);
        }

        return items.TryGetProperty("properties", out _)
            || (items.TryGetProperty("type", out var itemType) && itemType.GetString() == "object");
    }

    private static async Task<List<string>> PageIdsAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        HttpAssert.IsOk(response);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("data").EnumerateArray()
            .Select(order => order.GetProperty("id").GetString()!)
            .ToList();
    }
}
