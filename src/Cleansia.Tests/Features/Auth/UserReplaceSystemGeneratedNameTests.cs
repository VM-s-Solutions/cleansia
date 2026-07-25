using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// The domain rule behind the Apple returning-user back-fill: a name WE generated (blank, or exactly
/// what the email derivation produces) may be displaced by a name the client genuinely supplied, and a
/// name the user set themselves never may. Per-part, never all-or-nothing. Lives in
/// <see cref="User.ReplaceSystemGeneratedName"/> so no caller can implement a looser variant.
/// Written red → green per knowledge/testing.md.
/// </summary>
public class UserReplaceSystemGeneratedNameTests
{
    private static User UserWithName(string firstName, string lastName) =>
        UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            FirstName = firstName,
            LastName = lastName
        });

    [Fact]
    public void Replaces_A_Derived_Name_With_The_Supplied_Name()
    {
        var user = UserWithName("Cmisa", "Customer");

        user.ReplaceSystemGeneratedName("Michael", "Chaban", "Cmisa", "Customer");

        Assert.Equal("Michael", user.FirstName);
        Assert.Equal("Chaban", user.LastName);
    }

    [Fact]
    public void Does_Not_Replace_A_User_Edited_Name()
    {
        var user = UserWithName("Miguel", "Chabanov");

        user.ReplaceSystemGeneratedName("Michael", "Chaban", "Cmisa", "Customer");

        Assert.Equal("Miguel", user.FirstName);
        Assert.Equal("Chabanov", user.LastName);
    }

    // Per-part: the placeholder family name goes, the given name the user typed stays.
    [Fact]
    public void Replaces_Only_The_System_Generated_Part()
    {
        var user = UserWithName("Miguel", "Customer");

        user.ReplaceSystemGeneratedName("Michael", "Chaban", "Cmisa", "Customer");

        Assert.Equal("Miguel", user.FirstName);
        Assert.Equal("Chaban", user.LastName);
    }

    // The stored value came out of the same derivation, so casing/whitespace drift (an admin edit, an
    // older derivation) must not make it look user-authored.
    [Fact]
    public void Recognises_A_Derived_Name_Regardless_Of_Case()
    {
        var user = UserWithName("cmisa", "CUSTOMER");

        user.ReplaceSystemGeneratedName("Michael", "Chaban", "Cmisa", "Customer");

        Assert.Equal("Michael", user.FirstName);
        Assert.Equal("Chaban", user.LastName);
    }

    [Fact]
    public void Fills_A_Blank_Name_From_The_Supplied_Name()
    {
        var user = UserWithName(string.Empty, string.Empty);

        user.ReplaceSystemGeneratedName("Michael", "Chaban", "Cmisa", "Customer");

        Assert.Equal("Michael", user.FirstName);
        Assert.Equal("Chaban", user.LastName);
    }

    // Existing self-healing behaviour: with nothing supplied, a blank part still falls back to the
    // derivation, otherwise the account can never satisfy the profile-complete gate again.
    [Fact]
    public void Fills_A_Blank_Name_From_The_Derived_Name_When_Nothing_Is_Supplied()
    {
        var user = UserWithName(string.Empty, string.Empty);

        user.ReplaceSystemGeneratedName(null, null, "Cmisa", "Customer");

        Assert.Equal("Cmisa", user.FirstName);
        Assert.Equal("Customer", user.LastName);
    }

    // No supplied part means no genuine first-authorization payload: a stored system-generated name is
    // left exactly as it is rather than rewritten for no new information.
    [Fact]
    public void Leaves_A_System_Generated_Name_Untouched_When_Nothing_Is_Supplied()
    {
        var user = UserWithName("Cmisa", "Customer");

        user.ReplaceSystemGeneratedName(null, null, "Cmisa", "Customer");

        Assert.Equal("Cmisa", user.FirstName);
        Assert.Equal("Customer", user.LastName);
    }

    [Fact]
    public void Trims_The_Supplied_Name_And_Ignores_A_Whitespace_Only_One()
    {
        var user = UserWithName("Cmisa", "Customer");

        user.ReplaceSystemGeneratedName("  Michael  ", "   ", "Cmisa", "Customer");

        Assert.Equal("Michael", user.FirstName);
        Assert.Equal("Customer", user.LastName);
    }
}
