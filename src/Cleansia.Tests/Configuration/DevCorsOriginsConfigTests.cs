using Microsoft.Extensions.Configuration;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// Fully-local dev authenticates through same-site HttpOnly cookies, so a browser origin missing
/// from a host's effective <c>CorsOrigins</c> kills every preflighted call with an opaque 401. The
/// lists drifted once: admin allowed only 4200 while its documented concurrent-serve port is 4201,
/// and customer carried its own API origins (http:5003 / https:8003) — dead entries, since
/// same-origin requests are never CORS-gated.
///
/// The localhost origins live in <c>appsettings.Development.json</c> — NOT the base file — because
/// .NET merges JSON arrays BY INDEX: a base array longer than an environment override leaks its
/// tail indexes into that environment (a 2-entry base + 1-entry Production = Production serving
/// credentialed CORS to a localhost origin). These tests compose the configuration exactly as the
/// host does (base, then environment file) and pin both directions: Development covers the
/// documented dev origins, and NO environment composition ever carries a localhost or API
/// self-origin where it doesn't belong.
/// </summary>
public class DevCorsOriginsConfigTests
{
    [Theory]
    [InlineData("Cleansia.Web.Partner", new[] { "http://localhost:4200", "http://localhost:4201" })]
    [InlineData("Cleansia.Web.Admin", new[] { "http://localhost:4200", "http://localhost:4201" })]
    [InlineData(
        "Cleansia.Web.Customer",
        new[] { "http://localhost:4000", "http://localhost:4200", "http://localhost:4202" })]
    public void Development_CorsOrigins_Cover_The_Documented_Dev_Origins(
        string hostProject, string[] requiredOrigins)
    {
        var origins = LoadComposedCorsOrigins(hostProject, "Development");

        foreach (var origin in requiredOrigins)
        {
            Assert.Contains(origin, origins);
        }
    }

    [Theory]
    [InlineData("Cleansia.Web.Partner")]
    [InlineData("Cleansia.Web.Admin")]
    [InlineData("Cleansia.Web.Customer")]
    public void Production_CorsOrigins_Carry_No_Localhost_Origins(string hostProject)
    {
        // The index-merge guard: if a localhost origin ever reappears in the base file (or a
        // base array grows past the Production override's length), the leaked entry shows up
        // here exactly as the Production host would serve it.
        var origins = LoadComposedCorsOrigins(hostProject, "Production");

        Assert.NotEmpty(origins);
        foreach (var origin in origins)
        {
            Assert.DoesNotContain("localhost", origin);
        }
    }

    [Theory]
    [InlineData("Cleansia.Web.Partner")]
    [InlineData("Cleansia.Web.Admin")]
    [InlineData("Cleansia.Web.Customer")]
    public void No_Environment_Carries_Api_Self_Origins(string hostProject)
    {
        string[] apiSelfOrigins =
        [
            "http://localhost:5000",
            "http://localhost:5001",
            "http://localhost:5002",
            "http://localhost:5003",
            "http://localhost:5004",
            "https://localhost:8000",
            "https://localhost:8003",
        ];

        foreach (var environment in new[] { "Development", "Production" })
        {
            var origins = LoadComposedCorsOrigins(hostProject, environment);

            foreach (var apiOrigin in apiSelfOrigins)
            {
                Assert.DoesNotContain(apiOrigin, origins);
            }
        }
    }

    // Composes the list exactly as the host does — appsettings.json first, then the environment
    // file (index-merge semantics included) — and reads it the way CleansiaStartupBase does:
    // GetSection("CorsOrigins").Get<string[]>().
    private static string[] LoadComposedCorsOrigins(string hostProject, string environment)
    {
        var solutionDir = FindSolutionDirectory(AppContext.BaseDirectory);
        Assert.False(solutionDir is null, "Could not locate the solution directory from the test base directory.");

        var basePath = Path.Combine(solutionDir!, hostProject, "appsettings.json");
        Assert.True(File.Exists(basePath), $"Host settings not found: {basePath}");
        var envPath = Path.Combine(solutionDir!, hostProject, $"appsettings.{environment}.json");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(basePath)
            .AddJsonFile(envPath, optional: true)
            .Build();
        return configuration.GetSection("CorsOrigins").Get<string[]>() ?? [];
    }

    // Mirrors DatabaseMigrationExtensions.FindSolutionDirectory — walk up until a *.sln is found.
    private static string? FindSolutionDirectory(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
