using System.Reflection;
using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Common;

namespace Cleansia.Tests.Common;

/// <summary>
/// Every <see cref="BusinessErrorMessage"/> constant is emitted by name somewhere outside its own file.
/// A constant nothing emits is not an error the platform can answer, yet each one obliges the three
/// web apps to carry five translations the parity specs then assert on — thirty-six of them had
/// accumulated before this guard existed. The scan is by name, not by dot-value: a key written as a
/// bare literal is <c>BusinessErrorSlotContractTests</c>' concern, and a constant that is only ever
/// equal to another constant's value is still dead.
/// </summary>
public class BusinessErrorMessageEmittedTests
{
    private static readonly string[] ScannedProjectPrefixes = ["Cleansia.Core.AppServices", "Cleansia.Infra."];

    private static readonly Regex Reference = new(@"\bBusinessErrorMessage\.(\w+)\b", RegexOptions.Compiled);

    private static IReadOnlyList<string> DeclaredConstants() =>
        typeof(BusinessErrorMessage)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    private static HashSet<string> ReferencedConstants()
    {
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in SourceFiles())
        {
            foreach (Match match in Reference.Matches(File.ReadAllText(file)))
            {
                referenced.Add(match.Groups[1].Value);
            }
        }
        return referenced;
    }

    [Fact]
    public void The_Scan_Reads_Real_Source_And_A_Real_Number_Of_References()
    {
        var referenced = ReferencedConstants();

        Assert.True(referenced.Count > 200, $"only {referenced.Count} distinct constants referenced — the scan is not reading the tree");
        Assert.Contains(nameof(BusinessErrorMessage.NotFound), referenced);
        Assert.Contains(nameof(BusinessErrorMessage.OrderNotFound), referenced);
    }

    [Fact]
    public void Every_Constant_Is_Emitted_By_Name_Outside_Its_Own_File()
    {
        var referenced = ReferencedConstants();

        var dead = DeclaredConstants().Where(name => !referenced.Contains(name)).ToList();

        Assert.True(dead.Count == 0,
            "BusinessErrorMessage constants nothing in Cleansia.Core.AppServices or Cleansia.Infra.* emits — delete "
            + "each one with its api.* rows in the three web apps' five locales, or wire it to the refusal it names:\n  "
            + string.Join("\n  ", dead));
    }

    private static IEnumerable<string> SourceFiles()
    {
        var root = SrcRoot();
        var own = Path.Combine(root.FullName, "Cleansia.Core.AppServices", "Common", "BusinessErrorMessage.cs");
        Assert.True(File.Exists(own), $"BusinessErrorMessage.cs not found at {own}");

        var projects = root.EnumerateDirectories()
            .Where(d => ScannedProjectPrefixes.Any(prefix => d.Name.StartsWith(prefix, StringComparison.Ordinal)))
            .Where(d => !d.Name.EndsWith("Tests", StringComparison.Ordinal))
            .ToList();
        Assert.True(projects.Count >= 4, "Expected Cleansia.Core.AppServices and the Cleansia.Infra.* projects under src/");

        return projects
            .SelectMany(d => d.EnumerateFiles("*.cs", SearchOption.AllDirectories))
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(f => f.FullName)
            .Where(f => !string.Equals(f, own, StringComparison.OrdinalIgnoreCase));
    }

    private static DirectoryInfo SrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cleansia.Api.sln")))
            dir = dir.Parent;
        Assert.True(dir is not null, "Could not locate src/ root (Cleansia.Api.sln).");
        return dir!;
    }
}
