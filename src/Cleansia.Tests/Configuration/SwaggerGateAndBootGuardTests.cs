using Cleansia.Config.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// T-0123 / BSP-5 (AC2 + AC3) — the Swagger fail-closed gate and its ADR-0003 D3 boot guard.
///
/// AC2: Swagger/SwaggerUI must mount ONLY in Development. The old <c>!env.IsProduction()</c> gate
/// (CleansiaStartupBase.cs:103) leaked the full API surface on Staging / QA / Demo and on any mis-set
/// ASPNETCORE_ENVIRONMENT. <see cref="CleansiaStartupBase.SwaggerShouldServe"/> encodes the new
/// allow-list: true for Development, false for every other env string (Production, Staging, QA, Demo,
/// or unrecognized).
///
/// AC3: even in the Development branch, if <c>CorsOrigins</c> carries a public <c>cleansia.cz</c>
/// origin (a prod-shaped config running under a mis-set env string), the host must REFUSE TO BOOT.
/// <see cref="CleansiaStartupBase.GuardSwaggerExposure"/> is the pure D3-style guard (mirrors
/// <c>RateLimitPolicies.ValidateForwardedHeadersConfig</c>): it throws when Swagger would serve AND a
/// public cleansia.cz origin is present, and is a no-op otherwise.
/// Written red -> green per knowledge/testing.md (both members predate the gate/guard implementation).
/// </summary>
public class SwaggerGateAndBootGuardTests
{
    // ---- AC2: the env allow-list (Development only) -------------------------------------------------

    [Fact]
    public void Swagger_Serves_In_Development()
    {
        Assert.True(CleansiaStartupBase.SwaggerShouldServe(Environments.Development));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("QA")]
    [InlineData("Demo")]
    [InlineData("")]
    [InlineData("dEvElOpMeNt-typo")] // unrecognized / mis-set env string → fail closed
    public void Swagger_Does_Not_Serve_In_Any_Non_Development_Env(string env)
    {
        Assert.False(CleansiaStartupBase.SwaggerShouldServe(env));
    }

    // ---- AC3: the boot guard ------------------------------------------------------------------------

    [Theory]
    [InlineData("https://cleansia.cz")]
    [InlineData("https://www.cleansia.cz")]
    [InlineData("https://admin.cleansia.cz")]
    [InlineData("https://partner.cleansia.cz")]
    public void Boot_Refuses_When_Swagger_Would_Serve_And_Public_Cleansia_Origin_Present(string origin)
    {
        var ex = Record.Exception(() =>
            CleansiaStartupBase.GuardSwaggerExposure(swaggerWouldServe: true, corsOrigins: new[] { origin }));
        Assert.NotNull(ex);
    }

    [Fact]
    public void Boot_Is_Clean_When_Swagger_Would_Serve_But_Origins_Are_Local()
    {
        var ex = Record.Exception(() =>
            CleansiaStartupBase.GuardSwaggerExposure(
                swaggerWouldServe: true,
                corsOrigins: new[] { "http://localhost:4202", "http://localhost:4201" }));
        Assert.Null(ex);
    }

    [Fact]
    public void Boot_Is_Clean_When_Swagger_Off_Even_With_Public_Cleansia_Origin()
    {
        // Non-Development (Swagger off) + the real prod CORS list must boot normally.
        var ex = Record.Exception(() =>
            CleansiaStartupBase.GuardSwaggerExposure(
                swaggerWouldServe: false,
                corsOrigins: new[] { "https://cleansia.cz", "https://www.cleansia.cz" }));
        Assert.Null(ex);
    }

    [Fact]
    public void Boot_Is_Clean_When_Swagger_Would_Serve_And_No_Origins()
    {
        var ex = Record.Exception(() =>
            CleansiaStartupBase.GuardSwaggerExposure(swaggerWouldServe: true, corsOrigins: Array.Empty<string>()));
        Assert.Null(ex);
    }
}
