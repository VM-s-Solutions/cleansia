using System.Net.Http.Headers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.HostTests.Infrastructure;

/// <summary>
/// Base for every end-to-end host authz test. Owns the four real API hosts (one per JWT audience),
/// each booted via <see cref="HostTestApplicationFactory{TEntryPoint}"/> against the shared
/// Testcontainers Postgres, and provides:
/// <list type="bullet">
///   <item><see cref="ResetDatabaseAsync"/> — Respawn-truncate so each test arranges from a clean DB,</item>
///   <item><see cref="SeedAsync"/> — write an entity graph through a real host DbContext scope (so the
///   tenant-stamping + EF config are the production ones), and</item>
///   <item><c>*Client(token)</c> helpers — an <see cref="HttpClient"/> against a host carrying a real
///   bearer token.</item>
/// </list>
/// Hosts boot lazily on first use to keep test classes that only touch one host cheap.
/// </summary>
[Collection(HostTestCollection.Name)]
public abstract class AuthzHostTestBase : IAsyncLifetime
{
    protected readonly HostTestPostgresFixture Db;

    private readonly Lazy<HostTestApplicationFactory<Cleansia.Web.Admin.Program>> _admin;
    private readonly Lazy<HostTestApplicationFactory<Cleansia.Web.Partner.Program>> _partner;
    private readonly Lazy<HostTestApplicationFactory<Cleansia.Web.Customer.Program>> _customer;
    private readonly Lazy<HostTestApplicationFactory<Cleansia.Web.Mobile.Partner.Program>> _mobile;

    protected AuthzHostTestBase(HostTestPostgresFixture db)
    {
        Db = db;
        _admin = new(() => new HostTestApplicationFactory<Cleansia.Web.Admin.Program>(db.ConnectionString));
        _partner = new(() => new HostTestApplicationFactory<Cleansia.Web.Partner.Program>(db.ConnectionString));
        _customer = new(() => new HostTestApplicationFactory<Cleansia.Web.Customer.Program>(db.ConnectionString, ConfigureCustomerHostServices));
        _mobile = new(() => new HostTestApplicationFactory<Cleansia.Web.Mobile.Partner.Program>(db.ConnectionString));
    }

    protected HostTestApplicationFactory<Cleansia.Web.Admin.Program> AdminHost => _admin.Value;
    protected HostTestApplicationFactory<Cleansia.Web.Partner.Program> PartnerHost => _partner.Value;
    protected HostTestApplicationFactory<Cleansia.Web.Customer.Program> CustomerHost => _customer.Value;
    protected HostTestApplicationFactory<Cleansia.Web.Mobile.Partner.Program> MobileHost => _mobile.Value;

    // --- audiences (frozen in ADR-0001 D5 §2 / IJwtSettings.JwtAudiences) ---
    protected const string AdminAudience = "cleansia.admin";
    protected const string PartnerAudience = "cleansia.partner";
    protected const string CustomerAudience = "cleansia.customer";
    protected const string MobileAudience = "cleansia.mobile";

    public Task InitializeAsync() => ResetDatabaseAsync();

    public Task DisposeAsync()
    {
        if (_admin.IsValueCreated) _admin.Value.Dispose();
        if (_partner.IsValueCreated) _partner.Value.Dispose();
        if (_customer.IsValueCreated) _customer.Value.Dispose();
        if (_mobile.IsValueCreated) _mobile.Value.Dispose();
        return Task.CompletedTask;
    }

    protected Task ResetDatabaseAsync() => Db.ResetAsync();

    /// <summary>
    /// Override to swap a seam on the Customer host only (the harness has no Stripe stub by default:
    /// every other class exercises paths that stop before the outbound call).
    /// </summary>
    protected virtual void ConfigureCustomerHostServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Run <paramref name="seed"/> against a real host's <see cref="CleansiaDbContext"/> scope and
    /// commit. The Partner host is used by default — any host's scope writes the same shared DB.
    /// Seeding runs OUTSIDE an HTTP request, so the ambient tenant is set to
    /// <see cref="HostTestTenants.Default"/> for the commit: every stamped table is NOT NULL
    /// (ADR-0061 D8), and a row the seed did not stamp explicitly (the cross-tenant ACs do) lands there —
    /// the same company the default <see cref="TestJwtFactory"/> token carries.
    /// </summary>
    protected async Task SeedAsync(Func<CleansiaDbContext, Task> seed)
    {
        using var scope = PartnerHost.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(HostTestTenants.Default);
        var ctx = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        await seed(ctx);
        await ctx.CommitAsync(CancellationToken.None);
    }

    /// <summary>Read back from the DB ignoring the tenant query filter (for assertions that must see
    /// a row regardless of the seeded tenant — e.g. "no Dispute row was created").</summary>
    protected async Task<T> QueryAsync<T>(Func<CleansiaDbContext, Task<T>> query)
    {
        using var scope = PartnerHost.Services.CreateScope();
        // Override to a sentinel tenant the test data never uses so the default null/null single-tenant
        // match doesn't accidentally satisfy the filter; assertions use IgnoreQueryFilters anyway.
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        tenant.SetTenantOverride("__hosttests_query_scope__");
        var ctx = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        return await query(ctx);
    }

    protected HttpClient PartnerClient(string token) => Authorized(PartnerHost.CreateClient(), token);
    protected HttpClient CustomerClient(string token) => Authorized(CustomerHost.CreateClient(), token);
    protected HttpClient AdminClient(string token) => Authorized(AdminHost.CreateClient(), token);
    protected HttpClient MobileClient(string token) => Authorized(MobileHost.CreateClient(), token);

    protected HttpClient PartnerClientAnonymous() => PartnerHost.CreateClient();
    protected HttpClient CustomerClientAnonymous() => CustomerHost.CreateClient();

    private static HttpClient Authorized(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
