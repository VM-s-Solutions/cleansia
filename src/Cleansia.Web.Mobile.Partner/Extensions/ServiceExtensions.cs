using System.Security.Claims;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.OpenApi;
using System.Text;
using Cleansia.Config;
using Cleansia.Config.Services;
using Cleansia.Config.Services.DeviceRevocation;
using Cleansia.Config.Services.UserRevocation;
using Cleansia.Infra.Database;
using Cleansia.Web.Mobile.Partner.Middlewares;
using Cleansia.Web.Mobile.Partner.SwaggerSchemaFilters;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Infra.Common.Configuration.Interfaces;

namespace Cleansia.Web.Mobile.Partner.Extensions;

public static class ServiceExtensions
{
    public static void AddServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
    {
        services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider(JwtAudiences.Mobile));

        services
            .AddHttpContextAccessor()
            .AddCoreBindings(configuration, env)
            .AddMiddlewares()
            .AddApiVersioningServices()
            .AddSwagger()
            .AddJwt(configuration)
            .AddDeviceRevocationEnforcement(configuration)
            .AddUserRevocationEnforcement(configuration)
            .AddCleansiaAuthorization(configuration);
    }

    public static void MigrateDatabase(this IApplicationBuilder app, IWebHostEnvironment environment)
    {
        // In non-Development environments, migrations are applied by the CI/CD pipeline
        // (EF migrations bundle) before any API is deployed.
        if (!environment.IsDevelopment()) return;

        var scopeFactory = app.ApplicationServices.GetService<IServiceScopeFactory>();
        if (scopeFactory is null) return;

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        dbContext.Migrate();
    }

    private static IServiceCollection AddMiddlewares(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }

    private static IServiceCollection AddApiVersioningServices(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    private static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Cleansia.Mobile.Partner.API", Version = "v1" });
            c.CustomSchemaIds(type => GetSchemaId(type));
            c.SchemaFilter<EnumSchemaFilter>();
            c.SchemaFilter<CreateOrderNullableCustomerAddressFilter>();
            // OperationId becomes the generated client method name. The
            // default `ActionDescriptor.DisplayName` is the fully-qualified
            // controller path which produces awful Kotlin method names like
            // `cleansiaWebMobilePartnerControllersAuthControllerRegister...`.
            // The conventional `<Controller>_<Action>` form yields tidy
            // `authApi.authRegister(...)` style. ASP.NET only sets
            // ActionDescriptor.RouteValues["controller"] at MVC time so we
            // pull it from there.
            c.CustomOperationIds(apiDesc =>
            {
                var controller = apiDesc.ActionDescriptor.RouteValues["controller"];
                var action = (apiDesc.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)?.ActionName;
                return $"{controller}_{action}";
            });
        });

        return services;
    }

    /// <summary>
    /// Generates clean schema IDs for Swagger that are compatible with OpenAPI code generators.
    /// Handles generic types and nested types properly.
    /// </summary>
    private static string GetSchemaId(Type type)
    {
        var baseName = GetBaseTypeName(type);

        if (!type.IsGenericType)
        {
            return baseName;
        }

        var genericTypeName = baseName;

        var backtickIndex = genericTypeName.IndexOf('`');
        if (backtickIndex > 0)
        {
            genericTypeName = genericTypeName[..backtickIndex];
        }

        var genericArgs = type.GetGenericArguments();
        var argNames = string.Join("_", genericArgs.Select(GetSchemaId));

        return $"{genericTypeName}Of{argNames}";
    }

    /// <summary>
    /// Gets the base type name, including declaring type for nested types.
    /// E.g., RegisterEmployee+Command becomes RegisterEmployee_Command
    /// </summary>
    private static string GetBaseTypeName(Type type)
    {
        if (type.DeclaringType == null)
        {
            return type.Name;
        }

        return $"{GetBaseTypeName(type.DeclaringType)}_{type.Name}";
    }
    public static IServiceCollection AddJwt(this IServiceCollection services, IConfiguration configuration)
    {
        // Shared with the customer mobile host — the only difference was the audience (T-0420).
        return services.AddCleansiaMobileJwt(configuration, JwtAudiences.Mobile);
    }
}
