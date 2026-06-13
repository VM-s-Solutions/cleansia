using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// SavedAddress write-path isolation (testing.md must-cover #5, S3/S8) on the Customer host. A
/// SavedAddress is a per-USER resource-by-id: <c>UpdateSavedAddress</c>/<c>DeleteSavedAddress</c> guard
/// it with an existence check (tenant-filtered <c>GetByIdAsync</c>) followed by a
/// <c>BeOwnedByCaller</c> ownership check. Both boundary directions plus the happy path:
/// <list type="bullet">
///   <item>foreign-TENANT caller (sub = the genuine owner, token tenant differs) → the tenant filter
///   hides the row → existence fails → <c>NotFound</c>, row unchanged;</item>
///   <item>foreign-USER, SAME tenant → the row is visible but not theirs → <c>AddressNotOwnedByUser</c>,
///   row unchanged;</item>
///   <item>the legitimate owner → 200 and the row IS updated (proving the rejection is the boundary,
///   not a broken endpoint).</item>
/// </list>
/// Both tenants carry NON-NULL distinct tenant_id claims so the multi-tenant filter branch is the one
/// under test, not the single-tenant null/null escape.
/// </summary>
public sealed class Ac11CrossUserSavedAddressWriteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";

    private sealed record Arranged(string OwnerId, string OwnerEmail, string SavedAddressId);

    private async Task<Arranged> ArrangeOwnedSavedAddressAsync(string ownerTenant)
    {
        string ownerId = "", savedId = "";
        const string ownerEmail = "sa-owner@hosttests.local";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var owner = DomainSeed.Customer(ownerEmail, tenantId: ownerTenant);
            ctx.Users.Add(owner);
            var (address, saved) = DomainSeed.SavedAddressFor(owner.Id, "Home", tenantId: ownerTenant);
            ctx.Addresses.Add(address);
            ctx.SavedAddresses.Add(saved);
            ownerId = owner.Id;
            savedId = saved.Id;
        });
        return new Arranged(ownerId, ownerEmail, savedId);
    }

    private static HttpContent UpdateBody(string savedAddressId, string label) => JsonContent.Create(new
    {
        SavedAddressId = savedAddressId,
        Label = label,
        Street = "Updated St 11",
        City = "Ostrava",
        ZipCode = "70200",
        CountryId = (string?)null,
        Latitude = 49.8209,
        Longitude = 18.2625,
    });

    [Fact]
    public async Task Cross_tenant_update_saved_address_returns_not_found_and_leaves_the_label_unchanged()
    {
        var a = await ArrangeOwnedSavedAddressAsync(ownerTenant: TenantA);
        // sub = the genuine owner so the ownership gate WOULD pass; only the tenant differs.
        var token = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail,
            UserProfile.Customer, tenantId: TenantB);

        var resp = await CustomerClient(token).PutAsync("/api/SavedAddress/Update",
            UpdateBody(a.SavedAddressId, "Hijacked"));

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.NotFound);

        var label = await QueryAsync(ctx => ctx.Set<SavedAddress>()
            .IgnoreQueryFilters().Where(s => s.Id == a.SavedAddressId).Select(s => s.Label).FirstAsync());
        Assert.Equal("Home", label);
    }

    [Fact]
    public async Task Cross_user_same_tenant_update_saved_address_is_rejected_and_leaves_the_label_unchanged()
    {
        var a = await ArrangeOwnedSavedAddressAsync(ownerTenant: TenantA);
        string outsiderId = "";
        const string outsiderEmail = "sa-outsider@hosttests.local";
        await SeedAsync(async ctx =>
        {
            var outsider = DomainSeed.Customer(outsiderEmail, tenantId: TenantA);
            ctx.Users.Add(outsider);
            outsiderId = outsider.Id;
        });

        // Same tenant as the owner → the row is visible; ownership is the gate that must reject.
        var token = TestJwtFactory.Mint(CustomerAudience, outsiderId, outsiderEmail,
            UserProfile.Customer, tenantId: TenantA);

        var resp = await CustomerClient(token).PutAsync("/api/SavedAddress/Update",
            UpdateBody(a.SavedAddressId, "Hijacked"));

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.AddressNotOwnedByUser);

        var label = await QueryAsync(ctx => ctx.Set<SavedAddress>()
            .IgnoreQueryFilters().Where(s => s.Id == a.SavedAddressId).Select(s => s.Label).FirstAsync());
        Assert.Equal("Home", label);
    }

    [Fact]
    public async Task Owner_updating_their_own_saved_address_succeeds_and_the_label_is_changed()
    {
        var a = await ArrangeOwnedSavedAddressAsync(ownerTenant: TenantA);
        var token = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail,
            UserProfile.Customer, tenantId: TenantA);

        var resp = await CustomerClient(token).PutAsync("/api/SavedAddress/Update",
            UpdateBody(a.SavedAddressId, "Work"));

        HttpAssert.IsOk(resp);

        var label = await QueryAsync(ctx => ctx.Set<SavedAddress>()
            .IgnoreQueryFilters().Where(s => s.Id == a.SavedAddressId).Select(s => s.Label).FirstAsync());
        Assert.Equal("Work", label);
    }

    [Fact]
    public async Task Cross_tenant_delete_saved_address_returns_not_found_and_leaves_the_row_active()
    {
        var a = await ArrangeOwnedSavedAddressAsync(ownerTenant: TenantA);
        var token = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail,
            UserProfile.Customer, tenantId: TenantB);

        var resp = await CustomerClient(token).DeleteAsync($"/api/SavedAddress/Delete/{a.SavedAddressId}");

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.NotFound);

        var stillActive = await QueryAsync(ctx => ctx.Set<SavedAddress>()
            .IgnoreQueryFilters().AnyAsync(s => s.Id == a.SavedAddressId && s.IsActive));
        Assert.True(stillActive);
    }

    [Fact]
    public async Task Cross_user_same_tenant_delete_saved_address_is_rejected_and_leaves_the_row_active()
    {
        var a = await ArrangeOwnedSavedAddressAsync(ownerTenant: TenantA);
        string outsiderId = "";
        const string outsiderEmail = "sa-del-outsider@hosttests.local";
        await SeedAsync(async ctx =>
        {
            var outsider = DomainSeed.Customer(outsiderEmail, tenantId: TenantA);
            ctx.Users.Add(outsider);
            outsiderId = outsider.Id;
        });

        var token = TestJwtFactory.Mint(CustomerAudience, outsiderId, outsiderEmail,
            UserProfile.Customer, tenantId: TenantA);

        var resp = await CustomerClient(token).DeleteAsync($"/api/SavedAddress/Delete/{a.SavedAddressId}");

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.AddressNotOwnedByUser);

        var stillActive = await QueryAsync(ctx => ctx.Set<SavedAddress>()
            .IgnoreQueryFilters().AnyAsync(s => s.Id == a.SavedAddressId && s.IsActive));
        Assert.True(stillActive);
    }
}
