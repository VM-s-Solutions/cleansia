using System.Security.Claims;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using System.Text;
using Cleansia.Config;
using Cleansia.Core.AppServices.Common;
using Cleansia.Infra.Database;
using Cleansia.Web.Admin.Middlewares;
using Cleansia.Web.Admin.SwaggerSchemaFilters;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Configuration.Interfaces;

namespace Cleansia.Web.Admin.Extensions;

public static class ServiceExtensions
{
    public static void AddServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
    {
        services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider(JwtAudiences.Admin));

        services
            .AddHttpContextAccessor()
            .AddCoreBindings(configuration, env)
            .AddMiddlewares()
            .AddApiVersioningServices()
            .AddSwagger()
            .AddJwt(configuration)
            .AddUserAuthorization();
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
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Cleansia.Admin.API", Version = "v1" });
            c.CustomSchemaIds(type => GetSchemaId(type));
            c.SchemaFilter<EnumSchemaFilter>();
            c.CustomOperationIds(e => e.ActionDescriptor.DisplayName);
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
        var secret = Encoding.UTF8.GetBytes(configuration["JwtSettings:Secret"]!);
        var issuer = configuration["JwtSettings:Issuer"] ?? "cleansia";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = JwtAudiences.Admin,
                ValidateIssuerSigningKey = true,
                ValidateActor = false,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(secret),
                ClockSkew = TimeSpan.Zero,
            };
            options.Events = new JwtBearerEvents()
            {
                OnAuthenticationFailed = context => Task.CompletedTask,
                // Cookie fallback for the admin web SPA. See the Customer
                // host's matching comment for the migration rationale.
                OnMessageReceived = context =>
                {
                    if (string.IsNullOrEmpty(context.Token)
                        && context.Request.Cookies.TryGetValue("admin_token", out var fromCookie))
                    {
                        context.Token = fromCookie;
                    }
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    if (context.Principal?.Identity is not ClaimsIdentity claimsIdentity)
                    {
                        return Task.CompletedTask;
                    }

                    var roleClaims = claimsIdentity.FindAll("role").ToList();
                    if (!roleClaims.Any())
                    {
                        roleClaims = claimsIdentity.FindAll("roles").ToList();
                    }

                    foreach (var roleClaim in roleClaims)
                    {
                        claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, roleClaim.Value));
                    }
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .Build())
            .AddPolicy("AdminOnly", policy => policy
                .RequireRole(UserRole.Admin));

        return services;
    }

    public static IServiceCollection AddUserAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(PhysicalPolicy.Authenticated,
                p => p.RequireAuthenticatedUser())
            .AddPolicy(PhysicalPolicy.EmployeeOrAdmin,
                p => p.RequireRole(
                    UserProfile.Employee.ToString(),
                    UserProfile.Administrator.ToString()))
            .AddPolicy(PhysicalPolicy.AdminOnly,
                p => p.RequireRole(UserProfile.Administrator.ToString()))
            .AddPolicy(PhysicalPolicy.OwnerOrElevated, p =>
                p.RequireAssertion(ctx =>
                {
                    var user = ctx.User;

                    if (user.IsInRole(UserProfile.Administrator.ToString()) ||
                        user.IsInRole(UserProfile.Employee.ToString()))
                        return true;

                    var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (ctx.Resource is HttpContext http &&
                        http.Request.RouteValues.TryGetValue("id", out var routeId))
                    {
                        return routeId?.ToString() == sub;
                    }

                    return false;
                }));

        return services;
    }
}