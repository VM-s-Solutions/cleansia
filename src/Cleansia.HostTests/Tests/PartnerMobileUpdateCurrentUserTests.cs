using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// <c>GET /api/User/GetCurrent</c> + <c>PUT /api/User/UpdateCurrentUser</c> on the MOBILE PARTNER host.
/// The host had no UserController at all, so a cleaner's <c>PreferredLanguageCode</c> — the stamp every
/// pay-period and reminder mail is rendered in — was frozen at whatever signup sent.
///
/// Exercises the host's OWN auth registration (ADR-0001 D4): the audience, the
/// <c>PhysicalPolicy.Authenticated</c> mapping for an <c>Employee</c> role claim, and the real model
/// binder. The load-bearing case is the mutation proof — a body naming another user's row must change
/// the CALLER's row and leave the victim's untouched (S1/S3).
/// </summary>
public sealed class PartnerMobileUpdateCurrentUserTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string SupportedLanguage = "uk";
    private const string SeededPhone = "+420777111222";
    private static readonly DateOnly SeededBirthDate = new(1990, 1, 1);

    private sealed record Seeded(string Id, string Email);

    private async Task<Seeded> SeedCleanerAsync(string email)
    {
        var id = "";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            if (!await ctx.Languages.IgnoreQueryFilters().AnyAsync(l => l.Code == SupportedLanguage))
            {
                ctx.Languages.Add(Language.Create(SupportedLanguage, "Ukrainian"));
            }

            var user = DomainSeed.EmployeeUser(email);
            ctx.Users.Add(user);
            id = user.Id;
        });
        return new Seeded(id, email);
    }

    private string TokenFor(Seeded cleaner) =>
        TestJwtFactory.Mint(MobileAudience, cleaner.Id, cleaner.Email, UserProfile.Employee);

    /// <summary>The shape the partner app puts on the wire — no <c>id</c>, because the caller is the JWT.</summary>
    private static HttpContent Body(
        string firstName = "Emp",
        string lastName = "Loyee",
        string phone = SeededPhone,
        string? languageCode = null,
        DateOnly? birthDate = null) =>
        JsonContent.Create(new
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phone,
            BirthDate = birthDate?.ToString("yyyy-MM-dd"),
            Photo = (object?)null,
            LanguageCode = languageCode,
            RemovePhoto = false,
        });

    private Task<User> ReadUserAsync(string id) => QueryAsync(ctx => ctx.Users
        .IgnoreQueryFilters().AsNoTracking().FirstAsync(u => u.Id == id));

    [Fact]
    public async Task Cleaner_can_persist_a_language_preference_on_the_partner_mobile_host()
    {
        var cleaner = await SeedCleanerAsync("pm-lang@hosttests.local");

        var resp = await MobileClient(TokenFor(cleaner)).PutAsync("/api/User/UpdateCurrentUser",
            Body(languageCode: SupportedLanguage));

        HttpAssert.IsOk(resp);
        Assert.Equal(SupportedLanguage, (await ReadUserAsync(cleaner.Id)).PreferredLanguageCode);
    }

    /// <summary>The mutation proof: the body's <c>id</c> is inert, so a caller cannot write another row.</summary>
    [Fact]
    public async Task A_body_naming_another_user_updates_the_caller_and_leaves_the_victim_untouched()
    {
        var caller = await SeedCleanerAsync("pm-caller@hosttests.local");
        var victim = await SeedCleanerAsync("pm-victim@hosttests.local");

        var resp = await MobileClient(TokenFor(caller)).PutAsync("/api/User/UpdateCurrentUser",
            JsonContent.Create(new
            {
                Id = victim.Id,
                FirstName = "Hij",
                LastName = "Acked",
                PhoneNumber = "+420777900101",
                BirthDate = (string?)null,
                Photo = (object?)null,
                LanguageCode = SupportedLanguage,
                RemovePhoto = false,
            }));

        HttpAssert.IsOk(resp);

        var storedVictim = await ReadUserAsync(victim.Id);
        Assert.Equal("Emp", storedVictim.FirstName);
        Assert.Equal(SeededPhone, storedVictim.PhoneNumber);
        Assert.NotEqual(SupportedLanguage, storedVictim.PreferredLanguageCode);

        var storedCaller = await ReadUserAsync(caller.Id);
        Assert.Equal("Hij", storedCaller.FirstName);
        Assert.Equal(SupportedLanguage, storedCaller.PreferredLanguageCode);
    }

    [Fact]
    public async Task An_unauthenticated_caller_cannot_update_a_profile()
    {
        var cleaner = await SeedCleanerAsync("pm-anon@hosttests.local");

        var resp = await MobileHost.CreateClient().PutAsync("/api/User/UpdateCurrentUser",
            Body(firstName: "Anon", languageCode: SupportedLanguage));

        HttpAssert.IsUnauthorized(resp);
        Assert.Equal("Emp", (await ReadUserAsync(cleaner.Id)).FirstName);
    }

    [Fact]
    public async Task An_unauthenticated_caller_cannot_read_a_profile()
    {
        await SeedCleanerAsync("pm-anon-read@hosttests.local");

        var resp = await MobileHost.CreateClient().GetAsync("/api/User/GetCurrent");

        HttpAssert.IsUnauthorized(resp);
    }

    [Fact]
    public async Task Cleaner_can_read_their_own_profile()
    {
        var cleaner = await SeedCleanerAsync("pm-read@hosttests.local");

        var resp = await MobileClient(TokenFor(cleaner)).GetAsync("/api/User/GetCurrent");

        HttpAssert.IsOk(resp);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains(cleaner.Email, body);
    }

    /// <summary>A language-only save must not cost the cleaner their phone number — which the handler
    /// also copies onto every order they are the customer of.</summary>
    [Fact]
    public async Task A_blank_phone_number_does_not_erase_the_stored_one()
    {
        var cleaner = await SeedCleanerAsync("pm-blank-phone@hosttests.local");

        var resp = await MobileClient(TokenFor(cleaner)).PutAsync("/api/User/UpdateCurrentUser",
            Body(phone: string.Empty, languageCode: SupportedLanguage));

        HttpAssert.IsOk(resp);

        var stored = await ReadUserAsync(cleaner.Id);
        Assert.Equal(SeededPhone, stored.PhoneNumber);
        Assert.Equal(SeededBirthDate, stored.BirthDate);
        Assert.Equal(SupportedLanguage, stored.PreferredLanguageCode);
    }

    /// <summary>PreferredLanguageCode is an FK onto Languages.Code, so an unsupported code has to be a
    /// business rejection — otherwise it is a constraint violation raised by the commit AFTER the
    /// handler returns, i.e. a 500 the client cannot act on.</summary>
    [Fact]
    public async Task An_unsupported_language_code_is_rejected_as_a_business_error()
    {
        var cleaner = await SeedCleanerAsync("pm-bad-lang@hosttests.local");

        var resp = await MobileClient(TokenFor(cleaner)).PutAsync("/api/User/UpdateCurrentUser",
            Body(languageCode: "xx"));

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.LanguageNotSupported);
        Assert.NotEqual("xx", (await ReadUserAsync(cleaner.Id)).PreferredLanguageCode);
    }
}
