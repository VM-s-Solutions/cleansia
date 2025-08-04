using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using System.Text;
using Cleansia.Config;
using Cleansia.Core.AppServices.Common;
using Cleansia.Infra.Database;
using Cleansia.Web.Middlewares;
using Cleansia.Web.SwaggerSchemaFilters;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Cleansia.Web.Extensions;

public static class ServiceExtensions
{
    public static void AddServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
    {
        services
            .AddHttpContextAccessor()
            .AddCoreBindings(configuration, env)
            .AddMiddlewares()
            .AddSwagger()
            .AddJwt(configuration);
    }

    public static void MigrateDatabase(this IApplicationBuilder app, IWebHostEnvironment environment)
    {
        var scopeFactory = app.ApplicationServices.GetService<IServiceScopeFactory>();
        if (scopeFactory is null)
        {
            return;
        }

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

    private static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Cleansia.API", Version = "v1" });
            c.CustomSchemaIds(type => type.FullName?.Replace("+", string.Empty));
            c.SchemaFilter<EnumSchemaFilter>();
            c.CustomOperationIds(e => e.ActionDescriptor.DisplayName);
        });

        return services;
    }
    public static IServiceCollection AddJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var secret = Encoding.UTF8.GetBytes(configuration["JwtSettings:Secret"]!);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                ValidateActor = false,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(secret),
                ClockSkew = TimeSpan.Zero,
            };
            options.Events = new JwtBearerEvents()
            {
                OnAuthenticationFailed = context => Task.CompletedTask,
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
                .RequireRole(UserRole.Admin)); // Define a named policy for clarity

        return services;
    }
}