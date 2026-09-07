using System.Text.RegularExpressions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The development administrator that <c>sql-scripts/insert_seed_data.sql</c> creates.
///
/// A fresh dev database used to cost three manual steps before anyone could open the admin app —
/// register, confirm the email, then run <c>set-admin-role.sql</c>. The seed does it now, which
/// means a precomputed password hash sits in a SQL file where nothing type-checks it.
///
/// Postgres cannot compute that hash itself: it is PBKDF2-SHA256 at 600 000 iterations and pgcrypto
/// is deliberately unavailable (Azure blocks it unless allow-listed — the same reason
/// <c>generate_ulid()</c> uses <c>md5(random())</c>). So the literal is read straight back out of
/// the seed file and run through the REAL <see cref="PasswordExtensions.VerifyPassword"/>. Change
/// the version prefix, the iteration count or the hash size and this fails here, rather than as a
/// login that silently refuses the only account that can reach the admin app.
/// </summary>
public class SeededAdminCredentialsTests
{
    private const string SeededEmail = "admin@cleansia.local";
    private const string SeededPassword = "Admin123!";

    [Fact]
    public void The_Seeded_Hash_Accepts_The_Documented_Password()
    {
        Assert.True(
            SeededPassword.VerifyPassword(SeededHashFromSeedFile()),
            $"The hash in insert_seed_data.sql no longer verifies '{SeededPassword}'. " +
            "Regenerate it with the current PasswordExtensions parameters.");
    }

    [Fact]
    public void The_Seeded_Hash_Refuses_Anything_Else()
    {
        // Guards the negative: a VerifyPassword that returned true unconditionally would pass the
        // test above and prove nothing.
        Assert.False("Admin123".VerifyPassword(SeededHashFromSeedFile()));
        Assert.False("".VerifyPassword(SeededHashFromSeedFile()));
    }

    [Fact]
    public void The_Seeded_Hash_Is_Current_And_Needs_No_Rehash()
    {
        // A legacy-format hash would still verify, and the app would quietly rewrite it on first
        // login. Seeding one would be shipping technical debt into every fresh database.
        Assert.False(PasswordExtensions.NeedsRehash(SeededHashFromSeedFile()));
    }

    [Fact]
    public void The_Seeded_Row_Is_An_Administrator_Who_Can_Sign_In()
    {
        var insert = SeedFile();
        var block = insert[insert.IndexOf("DEVELOPMENT ADMINISTRATOR", StringComparison.Ordinal)..];

        // Profile 100 is the only value that reaches the admin app, and an unconfirmed email is
        // refused at login — the two things set-admin-role.sql used to do by hand.
        Assert.Contains($"{(int)UserProfile.Administrator}, {(int)AuthenticationType.Internal}, true", block);
        Assert.Contains(SeededEmail, block);
        // Reserved suffix: it cannot resolve to a real mailbox, so this fixture can never receive
        // real mail or be mistaken for a live account.
        Assert.EndsWith(".local", SeededEmail);
    }

    /// <summary>
    /// The dev seed is the ONLY place this account may come from. If it ever appears somewhere the
    /// production migration path runs, that is a shipped credential, not a fixture.
    /// </summary>
    [Fact]
    public void The_Seeded_Admin_Exists_Only_In_The_Development_Seed()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(FindSolutionDirectory(AppContext.BaseDirectory)!, ".."));
        var offenders = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "src"), "*.sql", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains(SeededEmail, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"The seeded dev admin appears outside sql-scripts/: {string.Join(", ", offenders)}");
    }

    /// <summary>Reads the literal out of the seed file, so the test cannot drift from what ships.</summary>
    private static string SeededHashFromSeedFile()
    {
        var match = Regex.Match(SeedFile(), @"'(?<hash>v2\$[A-Za-z0-9+/=]+)'");
        Assert.True(match.Success, "No v2$ password hash found in insert_seed_data.sql.");
        return match.Groups["hash"].Value;
    }

    private static string SeedFile()
    {
        var solutionDir = FindSolutionDirectory(AppContext.BaseDirectory);
        Assert.False(solutionDir is null, "Could not locate the solution directory.");

        var path = Path.GetFullPath(Path.Combine(solutionDir!, "..", "sql-scripts", "insert_seed_data.sql"));
        Assert.True(File.Exists(path), $"Seed file not found: {path}");
        return File.ReadAllText(path);
    }

    private static string? FindSolutionDirectory(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
