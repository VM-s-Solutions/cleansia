using Cleansia.Core.AppServices.Mappers;
using Cleansia.TestUtilities.MockDataFactories.Users;

namespace Cleansia.Tests.Features.AdminUsers;

public class AdminUserMappersLastLoginTests
{
    private static readonly DateTimeOffset LoginAt = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapToAdminDetailDto_Surfaces_Recorded_LastLoginAt()
    {
        var user = UserMockFactory.Generate();
        user.RecordLogin(LoginAt);

        var dto = user.MapToAdminDetailDto();

        Assert.NotNull(dto);
        Assert.Equal(LoginAt, dto!.LastLoginAt);
    }

    [Fact]
    public void MapToAdminListItem_Surfaces_Recorded_LastLoginAt()
    {
        var user = UserMockFactory.Generate();
        user.RecordLogin(LoginAt);

        var item = user.MapToAdminListItem();

        Assert.NotNull(item);
        Assert.Equal(LoginAt, item!.LastLoginAt);
    }

    [Fact]
    public void MapToAdminDetailDto_LastLoginAt_Null_When_Never_Logged_In()
    {
        var user = UserMockFactory.Generate();

        var dto = user.MapToAdminDetailDto();

        Assert.NotNull(dto);
        Assert.Null(dto!.LastLoginAt);
    }
}
