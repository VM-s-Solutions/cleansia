using Cleansia.TestUtilities.MockDataFactories.Users;

namespace Cleansia.Tests.Domain.Users;

public class UserRecordLoginTests
{
    [Fact]
    public void RecordLogin_Sets_LastLoginAt()
    {
        var user = UserMockFactory.Generate();
        var at = new DateTimeOffset(2026, 6, 1, 8, 30, 0, TimeSpan.Zero);

        user.RecordLogin(at);

        Assert.Equal(at, user.LastLoginAt);
    }

    [Fact]
    public void RecordLogin_Overwrites_Previous_Value()
    {
        var user = UserMockFactory.Generate();
        var first = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var second = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        user.RecordLogin(first);
        user.RecordLogin(second);

        Assert.Equal(second, user.LastLoginAt);
    }

    [Fact]
    public void LastLoginAt_Is_Null_Before_Any_Login()
    {
        var user = UserMockFactory.Generate();

        Assert.Null(user.LastLoginAt);
    }
}
