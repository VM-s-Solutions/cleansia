using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The company settings surface on the Admin host (AdminTenantSettingsController), end to end against
/// the real auth/authz pipeline: a non-admin is 403'd at every route; an admin reads the whole
/// catalogue, sets one key, sees it in effect, resets it, and leaves one admin audit row per act under
/// the frozen labels with the value before and after; a key outside the catalogue and a value outside
/// its range are refused with their keys; and a second company's admin never sees, nor changes, the
/// first company's value — the row is stamped with the writer's own company.
/// </summary>
public sealed class TenantSettingsRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string GetAllRoute = "/api/AdminTenantSettings/get-all";
    private const string SetRoute = "/api/AdminTenantSettings/set";
    private const string Key = RetentionDefaults.CustomerAuditRetentionYearsKey;
    private const string AdminAId = "tenant-settings-admin-a";
    private const string AdminBId = "tenant-settings-admin-b";

    private static string ResetRoute(string key) => $"/api/AdminTenantSettings/reset/{key}";

    private static string AdminToken(string userId, string tenantId) =>
        TestJwtFactory.Mint(AdminAudience, userId, $"{userId}@hosttests.local", UserProfile.Administrator, tenantId: tenantId);

    private sealed record SettingsResponse(List<SettingRow> Settings);

    private sealed record SettingRow(string Key, string Category, TenantSettingValueType ValueType, string DefaultValue, string EffectiveValue, bool IsOverridden, int? Min, int? Max);

    private sealed record SetResponse(string Key, string Value);

    private Task<List<AdminActionAudit>> AdminRowsAsync() =>
        QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().OrderBy(a => a.OccurredOn).ToListAsync());

    [Fact]
    public async Task NonAdmin_Employee_is_403d_on_every_route()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "emp-1", "emp-1@hosttests.local", UserProfile.Employee);
        var client = AdminClient(token);

        HttpAssert.IsForbidden(await client.GetAsync(GetAllRoute));
        HttpAssert.IsForbidden(await client.PutAsJsonAsync(SetRoute, new { key = Key, value = "1" }));
        HttpAssert.IsForbidden(await client.DeleteAsync(ResetRoute(Key)));
        Assert.Empty(await QueryAsync(ctx => ctx.TenantConfigurations.IgnoreQueryFilters().ToListAsync()));
    }

    [Fact]
    public async Task NonAdmin_Customer_is_403d_on_the_read()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "cust-1", "cust-1@hosttests.local", UserProfile.Customer);

        HttpAssert.IsForbidden(await AdminClient(token).GetAsync(GetAllRoute));
    }

    [Fact]
    public async Task Anonymous_caller_is_401d_on_every_route()
    {
        var client = AdminHost.CreateClient();

        HttpAssert.IsUnauthorized(await client.GetAsync(GetAllRoute));
        HttpAssert.IsUnauthorized(await client.PutAsJsonAsync(SetRoute, new { key = Key, value = "1" }));
        HttpAssert.IsUnauthorized(await client.DeleteAsync(ResetRoute(Key)));
    }

    [Fact]
    public async Task Admin_reads_the_whole_catalogue_at_its_defaults()
    {
        var resp = await AdminClient(AdminToken(AdminAId, HostTestTenants.A)).GetAsync(GetAllRoute);

        HttpAssert.IsOk(resp);
        var body = await resp.Content.ReadFromJsonAsync<SettingsResponse>();
        Assert.NotNull(body);
        Assert.Equal(11, body!.Settings.Count);
        Assert.All(body.Settings, s =>
        {
            Assert.False(s.IsOverridden);
            Assert.Equal(s.DefaultValue, s.EffectiveValue);
        });
        Assert.Equal(9, body.Settings.Count(s => s.Category == "retention"));
        var mailbox = Assert.Single(body.Settings, s => s.Key == "notifications.admin_email");
        Assert.Equal("notifications", mailbox.Category);
        Assert.Equal(TenantSettingValueType.Email, mailbox.ValueType);
        Assert.Equal(string.Empty, mailbox.DefaultValue);
        Assert.Null(mailbox.Min);
        Assert.Null(mailbox.Max);
        var window = Assert.Single(body.Settings, s => s.Key == Key);
        Assert.Equal("3", window.DefaultValue);
        Assert.Equal(1, window.Min);
        Assert.Equal(100, window.Max);
        var horizon = Assert.Single(body.Settings, s => s.Key == "lifecycle.chargeback_horizon_days");
        Assert.Equal("lifecycle", horizon.Category);
        Assert.Equal("180", horizon.DefaultValue);
        Assert.Equal(0, horizon.Min);
        Assert.Equal(730, horizon.Max);
    }

    [Fact]
    public async Task Admin_sets_a_key_sees_it_in_effect_resets_it_and_each_act_leaves_one_audit_row()
    {
        var client = AdminClient(AdminToken(AdminAId, HostTestTenants.A));

        var set = await client.PutAsJsonAsync(SetRoute, new { key = Key, value = " 01 " });
        HttpAssert.IsOk(set);
        var setBody = await set.Content.ReadFromJsonAsync<SetResponse>();
        Assert.Equal(new SetResponse(Key, "1"), setBody);

        var afterSet = await (await client.GetAsync(GetAllRoute)).Content.ReadFromJsonAsync<SettingsResponse>();
        var window = Assert.Single(afterSet!.Settings, s => s.Key == Key);
        Assert.True(window.IsOverridden);
        Assert.Equal("1", window.EffectiveValue);
        Assert.Equal("3", window.DefaultValue);

        var stored = Assert.Single(await QueryAsync(ctx => ctx.TenantConfigurations.IgnoreQueryFilters().ToListAsync()));
        Assert.Equal(HostTestTenants.A, stored.TenantId);
        Assert.Equal("1", stored.Value);
        Assert.Equal("retention", stored.Category);

        var updated = await client.PutAsJsonAsync(SetRoute, new { key = Key, value = "2" });
        HttpAssert.IsOk(updated);
        Assert.Single(await QueryAsync(ctx => ctx.TenantConfigurations.IgnoreQueryFilters().ToListAsync()));

        var reset = await client.DeleteAsync(ResetRoute(Key));
        HttpAssert.IsOk(reset);
        Assert.Equal(new SetResponse(Key, "3"), await reset.Content.ReadFromJsonAsync<SetResponse>());
        Assert.Empty(await QueryAsync(ctx => ctx.TenantConfigurations.IgnoreQueryFilters().ToListAsync()));

        var afterReset = await (await client.GetAsync(GetAllRoute)).Content.ReadFromJsonAsync<SettingsResponse>();
        var restored = Assert.Single(afterReset!.Settings, s => s.Key == Key);
        Assert.False(restored.IsOverridden);
        Assert.Equal("3", restored.EffectiveValue);

        var rows = await AdminRowsAsync();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.True(row.Success);
            Assert.Equal(AdminAId, row.ActorId);
            Assert.Equal("TenantSetting", row.ResourceType);
            Assert.Equal(Key, row.ResourceId);
            Assert.Equal(HostTestTenants.A, row.TenantId);
        });
        Assert.Equal(["tenant_setting.set", "tenant_setting.set", "tenant_setting.reset"], rows.Select(r => r.Action));
        Assert.Equal((null, "1"), Snapshot(rows[0]));
        Assert.Equal(("1", "2"), Snapshot(rows[1]));
        Assert.Equal(("2", null), Snapshot(rows[2]));
    }

    [Fact]
    public async Task The_admin_mailbox_refuses_what_is_not_an_address_stores_a_valid_one_canonical_and_is_gone_after_reset()
    {
        var client = AdminClient(AdminToken(AdminAId, HostTestTenants.A));
        var key = TenantSettingCatalog.AdminNotificationEmailKey;

        var malformed = await client.PutAsJsonAsync(SetRoute, new { key, value = "not an address" });
        await HttpAssert.AssertBusinessErrorAsync(malformed, BusinessErrorMessage.TenantSettingInvalidValue);

        var empty = await client.PutAsJsonAsync(SetRoute, new { key, value = "" });
        await HttpAssert.AssertBusinessErrorAsync(empty, BusinessErrorMessage.Required);
        Assert.Empty(await QueryAsync(ctx => ctx.TenantConfigurations.IgnoreQueryFilters().ToListAsync()));

        var set = await client.PutAsJsonAsync(SetRoute, new { key, value = " Ops@Example.com " });
        HttpAssert.IsOk(set);
        Assert.Equal(new SetResponse(key, "ops@example.com"), await set.Content.ReadFromJsonAsync<SetResponse>());
        var stored = Assert.Single(await QueryAsync(ctx => ctx.TenantConfigurations.IgnoreQueryFilters().ToListAsync()));
        Assert.Equal(HostTestTenants.A, stored.TenantId);
        Assert.Equal("ops@example.com", stored.Value);
        Assert.Equal("notifications", stored.Category);

        var inEffect = await (await client.GetAsync(GetAllRoute)).Content.ReadFromJsonAsync<SettingsResponse>();
        var mailbox = Assert.Single(inEffect!.Settings, s => s.Key == key);
        Assert.True(mailbox.IsOverridden);
        Assert.Equal("ops@example.com", mailbox.EffectiveValue);

        var reset = await client.DeleteAsync(ResetRoute(key));
        HttpAssert.IsOk(reset);
        Assert.Equal(new SetResponse(key, string.Empty), await reset.Content.ReadFromJsonAsync<SetResponse>());
        Assert.Empty(await QueryAsync(ctx => ctx.TenantConfigurations.IgnoreQueryFilters().ToListAsync()));
        var restored = Assert.Single((await (await client.GetAsync(GetAllRoute)).Content.ReadFromJsonAsync<SettingsResponse>())!.Settings, s => s.Key == key);
        Assert.False(restored.IsOverridden);
        Assert.Equal(string.Empty, restored.EffectiveValue);
    }

    [Fact]
    public async Task A_key_outside_the_catalogue_and_a_value_outside_its_range_are_refused_with_their_keys()
    {
        var client = AdminClient(AdminToken(AdminAId, HostTestTenants.A));

        var unknown = await client.PutAsJsonAsync(SetRoute, new { key = "retention.unknown.years", value = "1" });
        await HttpAssert.AssertBusinessErrorAsync(unknown, BusinessErrorMessage.TenantSettingUnknownKey);

        var outOfRange = await client.PutAsJsonAsync(SetRoute, new { key = Key, value = "0" });
        await HttpAssert.AssertBusinessErrorAsync(outOfRange, BusinessErrorMessage.TenantSettingInvalidValue);

        var unknownReset = await client.DeleteAsync(ResetRoute("retention.unknown.years"));
        await HttpAssert.AssertBusinessErrorAsync(unknownReset, BusinessErrorMessage.TenantSettingUnknownKey);

        Assert.Empty(await QueryAsync(ctx => ctx.TenantConfigurations.IgnoreQueryFilters().ToListAsync()));
        var refusals = await AdminRowsAsync();
        Assert.Equal(3, refusals.Count);
        Assert.All(refusals, row => Assert.False(row.Success));
        Assert.Equal(
            [BusinessErrorMessage.TenantSettingUnknownKey, BusinessErrorMessage.TenantSettingInvalidValue, BusinessErrorMessage.TenantSettingUnknownKey],
            refusals.Select(r => r.ErrorCode));
    }

    [Fact]
    public async Task A_second_companys_admin_neither_sees_nor_changes_the_first_companys_value()
    {
        var adminA = AdminClient(AdminToken(AdminAId, HostTestTenants.A));
        var adminB = AdminClient(AdminToken(AdminBId, HostTestTenants.B));

        HttpAssert.IsOk(await adminA.PutAsJsonAsync(SetRoute, new { key = Key, value = "1" }));

        var seenByB = await (await adminB.GetAsync(GetAllRoute)).Content.ReadFromJsonAsync<SettingsResponse>();
        var windowForB = Assert.Single(seenByB!.Settings, s => s.Key == Key);
        Assert.False(windowForB.IsOverridden);
        Assert.Equal("3", windowForB.EffectiveValue);

        HttpAssert.IsOk(await adminB.PutAsJsonAsync(SetRoute, new { key = Key, value = "2" }));
        HttpAssert.IsOk(await adminB.DeleteAsync(ResetRoute(Key)));

        var stored = Assert.Single(await QueryAsync(ctx => ctx.TenantConfigurations.IgnoreQueryFilters().ToListAsync()));
        Assert.Equal(HostTestTenants.A, stored.TenantId);
        Assert.Equal("1", stored.Value);

        var seenByA = await (await adminA.GetAsync(GetAllRoute)).Content.ReadFromJsonAsync<SettingsResponse>();
        var windowForA = Assert.Single(seenByA!.Settings, s => s.Key == Key);
        Assert.True(windowForA.IsOverridden);
        Assert.Equal("1", windowForA.EffectiveValue);
    }

    private static (string? Before, string? After) Snapshot(AdminActionAudit row)
    {
        return (Value(row.BeforeJson), Value(row.AfterJson));

        static string? Value(string? json)
        {
            Assert.NotNull(json);
            using var doc = JsonDocument.Parse(json!);
            var root = doc.RootElement;
            Assert.Equal(Key, root.GetProperty("key").GetString());
            var value = root.GetProperty("value");
            return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
        }
    }
}
