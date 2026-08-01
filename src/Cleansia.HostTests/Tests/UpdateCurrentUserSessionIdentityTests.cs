using System.Net.Http.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// End-to-end identity contract for <c>PUT /api/User/UpdateCurrentUser</c> on the Customer host: the
/// row written is ALWAYS the JWT caller's, and the body's <c>id</c> is inert (S1).
///
/// The web client cannot supply an id — the session is an HttpOnly cookie so JS cannot read the JWT,
/// and <c>MyProfileDto</c> carries none — so it sends the body with no <c>id</c> member at all. That
/// path had no test; only unit tests that handed the validator a matching id. These cover it through
/// the real MVC model binder, so a re-introduced dependency on the client-supplied id fails here
/// whichever layer it lands in (implicit model-binding required, validator, or handler).
/// </summary>
public sealed class UpdateCurrentUserSessionIdentityTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private sealed record Seeded(string Id, string Email);

    private async Task<Seeded> SeedCustomerAsync(string email)
    {
        string id = "";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var user = DomainSeed.Customer(email);
            ctx.Users.Add(user);
            id = user.Id;
        });
        return new Seeded(id, email);
    }

    /// <summary>The body the Angular customer app actually puts on the wire: NSwag leaves
    /// <c>id</c> undefined and <c>JSON.stringify</c> drops it, so the member is absent entirely.</summary>
    private static HttpContent WebShapedBody(string firstName, string lastName, string phone) =>
        JsonContent.Create(new
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phone,
            BirthDate = (string?)null,
            Photo = (object?)null,
            LanguageCode = (string?)null,
            RemovePhoto = false,
        });

    private static HttpContent BodyWithId(string id, string firstName, string lastName, string phone) =>
        JsonContent.Create(new
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phone,
            BirthDate = (string?)null,
            Photo = (object?)null,
            LanguageCode = (string?)null,
            RemovePhoto = false,
        });

    private Task<User> ReadUserAsync(string id) => QueryAsync(ctx => ctx.Users
        .IgnoreQueryFilters().AsNoTracking().FirstAsync(u => u.Id == id));

    [Fact]
    public async Task Save_with_no_id_at_all_updates_the_callers_own_row()
    {
        var caller = await SeedCustomerAsync("ucu-web@hosttests.local");
        var token = TestJwtFactory.Mint(CustomerAudience, caller.Id, caller.Email, UserProfile.Customer);

        var resp = await CustomerClient(token).PutAsync("/api/User/UpdateCurrentUser",
            WebShapedBody("Webby", "Saver", "+420777900001"));

        HttpAssert.IsOk(resp);

        var stored = await ReadUserAsync(caller.Id);
        Assert.Equal("Webby", stored.FirstName);
        Assert.Equal("Saver", stored.LastName);
        Assert.Equal("+420777900001", stored.PhoneNumber);
    }

    [Fact]
    public async Task Save_carrying_a_foreign_id_updates_the_caller_and_leaves_the_victim_untouched()
    {
        var caller = await SeedCustomerAsync("ucu-caller@hosttests.local");
        var victim = await SeedCustomerAsync("ucu-victim@hosttests.local");
        var token = TestJwtFactory.Mint(CustomerAudience, caller.Id, caller.Email, UserProfile.Customer);

        var resp = await CustomerClient(token).PutAsync("/api/User/UpdateCurrentUser",
            BodyWithId(victim.Id, "Hij", "Acked", "+420777900002"));

        HttpAssert.IsOk(resp);

        var storedVictim = await ReadUserAsync(victim.Id);
        Assert.Equal("Cust", storedVictim.FirstName);
        Assert.Equal("Omer", storedVictim.LastName);
        Assert.Null(storedVictim.PhoneNumber);

        var storedCaller = await ReadUserAsync(caller.Id);
        Assert.Equal("Hij", storedCaller.FirstName);
    }

    [Fact]
    public async Task Save_carrying_a_blank_id_updates_the_callers_own_row()
    {
        var caller = await SeedCustomerAsync("ucu-blank@hosttests.local");
        var token = TestJwtFactory.Mint(CustomerAudience, caller.Id, caller.Email, UserProfile.Customer);

        var resp = await CustomerClient(token).PutAsync("/api/User/UpdateCurrentUser",
            BodyWithId(string.Empty, "Blank", "Saver", "+420777900004"));

        HttpAssert.IsOk(resp);

        var stored = await ReadUserAsync(caller.Id);
        Assert.Equal("Blank", stored.FirstName);
    }

    [Fact]
    public async Task Save_carrying_the_callers_own_id_still_works()
    {
        var caller = await SeedCustomerAsync("ucu-mobile@hosttests.local");
        var token = TestJwtFactory.Mint(CustomerAudience, caller.Id, caller.Email, UserProfile.Customer);

        var resp = await CustomerClient(token).PutAsync("/api/User/UpdateCurrentUser",
            BodyWithId(caller.Id, "Mobi", "Saver", "+420777900003"));

        HttpAssert.IsOk(resp);

        var stored = await ReadUserAsync(caller.Id);
        Assert.Equal("Mobi", stored.FirstName);
    }
}
