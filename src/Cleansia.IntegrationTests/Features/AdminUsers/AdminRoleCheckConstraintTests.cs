using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.AdminUsers;

/// <summary>
/// ADR-0066 D1 on the emitted DDL: <c>CK_Users_AdminRole_Profile</c> refuses an administrator row
/// without a role and a customer or cleaner row with one, as a 23514 the factory's throw cannot be
/// bypassed around; a row that satisfies the invariant on either side lands.
/// </summary>
[Collection("PostgresCollection")]
public sealed class AdminRoleCheckConstraintTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AdminId = "ck-role-admin";
    private const string CustomerId = "ck-role-customer";

    private static Task Seed(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var admin = User.CreateWithPassword($"{AdminId}@cleansia.test", "Seed-Password-123", "Ad", "Min", UserProfile.Administrator, adminRole: AdminRole.Administrator);
        admin.Id = AdminId;
        var customer = User.CreateWithPassword($"{CustomerId}@cleansia.test", "Seed-Password-123", "Cust", "Omer");
        customer.Id = CustomerId;
        context.Users.AddRange(admin, customer);
        return Task.CompletedTask;
    }

    private static async Task<PostgresException> RefusedAsync(CleansiaDbContext context, FormattableString sql)
    {
        var thrown = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlAsync(sql));
        Assert.Equal(PostgresErrorCodes.CheckViolation, thrown.SqlState);
        Assert.Equal("CK_Users_AdminRole_Profile", thrown.ConstraintName);
        return thrown;
    }

    [Fact]
    public async Task An_Administrator_Without_A_Role_And_A_Customer_With_One_Both_Fail_The_Check()
    {
        await TestMethod(
            arrange: Seed,
            act: async provider =>
            {
                var context = provider.GetRequiredService<CleansiaDbContext>();
                await RefusedAsync(context, $"""UPDATE "Users" SET "AdminRole" = NULL WHERE "Id" = {AdminId}""");
                await RefusedAsync(context, $"""UPDATE "Users" SET "AdminRole" = {(int)AdminRole.Support} WHERE "Id" = {CustomerId}""");
                await RefusedAsync(context, $"""UPDATE "Users" SET "Profile" = {(int)UserProfile.Customer} WHERE "Id" = {AdminId}""");
                await RefusedAsync(context, $"""UPDATE "Users" SET "Profile" = {(int)UserProfile.Administrator} WHERE "Id" = {CustomerId}""");
                return 0;
            },
            assert: async (CleansiaDbContext context, int _) =>
            {
                var rows = await context.Users.IgnoreQueryFilters().AsNoTracking()
                    .Where(u => u.Id == AdminId || u.Id == CustomerId)
                    .ToDictionaryAsync(u => u.Id);
                Assert.Equal(AdminRole.Administrator, rows[AdminId].AdminRole);
                Assert.Equal(UserProfile.Administrator, rows[AdminId].Profile);
                Assert.Null(rows[CustomerId].AdminRole);
                Assert.Equal(UserProfile.Customer, rows[CustomerId].Profile);
            },
            transactional: false);
    }

    [Fact]
    public async Task Both_Columns_Moved_Together_Pass_The_Check()
    {
        await TestMethod(
            arrange: Seed,
            act: async provider =>
            {
                var context = provider.GetRequiredService<CleansiaDbContext>();
                return await context.Database.ExecuteSqlAsync(
                    $"""UPDATE "Users" SET "Profile" = {(int)UserProfile.Administrator}, "AdminRole" = {(int)AdminRole.Support} WHERE "Id" = {CustomerId}""");
            },
            assert: async (CleansiaDbContext context, int rows) =>
            {
                Assert.Equal(1, rows);
                var promoted = await context.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == CustomerId);
                Assert.Equal(UserProfile.Administrator, promoted.Profile);
                Assert.Equal(AdminRole.Support, promoted.AdminRole);
            },
            transactional: false);
    }
}
