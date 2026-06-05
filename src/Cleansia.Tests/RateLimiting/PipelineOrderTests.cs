namespace Cleansia.Tests.RateLimiting;

/// <summary>
/// ADR-0003 D4 / AC6 (verify #5) — the complete pipeline band, asserted against the single shared
/// <c>CleansiaStartupBase.Configure</c> source. Source-order is the right level here: the ordering is
/// a property of how <c>Configure</c> registers middleware (a built pipeline doesn't expose its
/// ordered delegate list for assertion), and the reviewer gate is literally about that ordering.
///
/// Target order (D4):
///   EnableBuffering → UseForwardedHeaders (NEW, top) → [Swagger] → RequestLogging →
///   UseExceptionHandler → UseRouting → UseCors → UseAuthentication → UseRateLimiter →
///   UseHostAuthMiddleware (CSRF, unchanged) → UseAuthorization → endpoints
/// </summary>
public class PipelineOrderTests
{
    private static string ConfigureSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cleansia.Api.sln")))
            dir = dir.Parent;
        Assert.True(dir is not null, "Could not locate src/ root.");
        var file = Path.Combine(dir!.FullName, "Cleansia.Config", "Abstractions", "CleansiaStartupBase.cs");
        Assert.True(File.Exists(file), $"CleansiaStartupBase.cs not found at {file}");
        return File.ReadAllText(file);
    }

    private static int IndexOf(string haystack, string needle)
    {
        var i = haystack.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(i >= 0, $"Expected to find '{needle}' in CleansiaStartupBase.Configure.");
        return i;
    }

    [Fact]
    public void UseForwardedHeaders_Precedes_RequestLogging_And_RateLimiter()
    {
        var src = ConfigureSource();
        var fwd = IndexOf(src, "UseForwardedHeaders(");
        var reqLog = IndexOf(src, "UseMiddleware(RequestLoggingMiddlewareType)");
        var rateLimiter = IndexOf(src, "UseRateLimiter(");

        Assert.True(fwd < reqLog, "UseForwardedHeaders must precede RequestLogging (so audit-log IP is correct).");
        Assert.True(fwd < rateLimiter, "UseForwardedHeaders must precede UseRateLimiter (so the per-IP key is the real client IP).");
    }

    [Fact]
    public void UseForwardedHeaders_Is_Immediately_After_EnableBuffering()
    {
        var src = ConfigureSource();
        var buffering = IndexOf(src, "EnableBuffering(");
        var fwd = IndexOf(src, "UseForwardedHeaders(");
        var swagger = IndexOf(src, "UseSwagger(");
        Assert.True(buffering < fwd, "UseForwardedHeaders must come after the EnableBuffering block.");
        Assert.True(fwd < swagger, "UseForwardedHeaders is at the top — before the Swagger block.");
    }

    [Fact]
    public void RateLimiter_Runs_After_Authentication()
    {
        var src = ConfigureSource();
        var auth = IndexOf(src, "UseAuthentication(");
        var rateLimiter = IndexOf(src, "UseRateLimiter(");
        Assert.True(auth < rateLimiter,
            "UseRateLimiter must run AFTER UseAuthentication so HttpContext.User is populated for the sub branch.");
    }

    [Fact]
    public void Csrf_HostAuthMiddleware_Stays_Between_Limiter_Band_And_Authorization()
    {
        var src = ConfigureSource();
        var rateLimiter = IndexOf(src, "UseRateLimiter(");
        // The CALL site in Configure is "UseHostAuthMiddleware(app)"; the virtual declaration
        // "UseHostAuthMiddleware(IApplicationBuilder app)" appears earlier — match the call form.
        var csrf = IndexOf(src, "UseHostAuthMiddleware(app)");
        var authorization = IndexOf(src, "UseAuthorization(");

        Assert.True(rateLimiter < csrf, "CSRF (UseHostAuthMiddleware) stays after the limiter band.");
        Assert.True(csrf < authorization, "CSRF (UseHostAuthMiddleware) stays before UseAuthorization (unchanged position).");
    }
}
