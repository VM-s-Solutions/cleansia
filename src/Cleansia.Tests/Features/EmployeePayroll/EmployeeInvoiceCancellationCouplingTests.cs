using System.Reflection;
using System.Text.RegularExpressions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// ADR-0046 §D4.3's remedy gate has three arms — no variable symbol, an assignable status, and not
/// cancelled — but <c>EmployeeInvoiceDetailDto</c> carries only the first two. The client's button is
/// nonetheless faithful, because <c>IsCancelled</c> and <c>Status == Cancelled</c> are written by the
/// same statement and are therefore the same fact. Safety does not depend on this (the server's own
/// gate reads the entity); FIDELITY does, so the coincidence is pinned here instead of widening a DTO.
///
/// <para>The pin is SOURCE-LEVEL rather than a sweep over the transitions, because a sweep can only
/// observe writers it can reach: a second writer guarded by a precondition the fixture does not
/// satisfy — <c>AdminVoid()</c> legal only from <c>Approved</c>, say — is invoked, throws, is skipped,
/// and the suite stays green. Both properties have non-public setters, so the declaring file is a
/// provably complete search space and a scan of it sees every writer whether or not any test can reach
/// it. That premise is asserted first, so making a setter public fails here rather than silently
/// hollowing the scan out.</para>
/// </summary>
public partial class EmployeeInvoiceCancellationCouplingTests
{
    [GeneratedRegex(@"(?<![A-Za-z0-9_])IsCancelled\s*=(?!=)")]
    private static partial Regex IsCancelledWrite();

    // The terminator admits ',' as well as ';' because a status is also written inside the object
    // initializer in Create.
    [GeneratedRegex(@"(?<![A-Za-z0-9_])Status\s*=(?!=)\s*(?<rhs>[^;,\r\n]+)\s*[;,]")]
    private static partial Regex StatusWrite();

    [Fact]
    public void Cancelling_Sets_Both_Halves_Of_The_Fact()
    {
        var invoice = PayrollMockFactory.Invoice();

        Assert.False(invoice.IsCancelled);
        Assert.NotEqual(EmployeeInvoiceStatus.Cancelled, invoice.Status);

        invoice.Cancel("duplicate", "admin@cleansia.cz");

        Assert.True(invoice.IsCancelled);
        Assert.Equal(EmployeeInvoiceStatus.Cancelled, invoice.Status);
    }

    [Fact]
    public void Both_Halves_Have_Non_Public_Setters_So_The_Declaring_File_Is_The_Whole_Search_Space()
    {
        AssertSetterIsNonPublic(nameof(EmployeeInvoice.IsCancelled));
        AssertSetterIsNonPublic(nameof(EmployeeInvoice.Status));
    }

    [Fact]
    public void IsCancelled_Is_Written_By_Cancel_And_By_Nothing_Else()
    {
        var (source, cancelBody) = ReadEntitySource();

        var writesInFile = IsCancelledWrite().Count(source);
        var writesInCancel = IsCancelledWrite().Count(cancelBody);

        Assert.Equal(1, writesInCancel);
        Assert.Equal(writesInCancel, writesInFile);
    }

    [Fact]
    public void The_Cancelled_Status_Is_Written_By_Cancel_And_By_Nothing_Else()
    {
        var (source, cancelBody) = ReadEntitySource();

        // A write from a variable would defeat the scan below, so it is refused outright: every status
        // write in this entity names its target status literally.
        var nonLiteralWrites = StatusWrite().Matches(source)
            .Select(m => m.Groups["rhs"].Value.Trim())
            .Where(rhs => !LiteralStatus().IsMatch(rhs))
            .ToList();

        Assert.True(nonLiteralWrites.Count == 0,
            "A status is assigned from something other than an EmployeeInvoiceStatus literal, so a " +
            "second writer of Cancelled can no longer be found by reading this entity:\n  " +
            string.Join("\n  ", nonLiteralWrites));

        var cancelledWritesInFile = CountCancelledStatusWrites(source);
        var cancelledWritesInCancel = CountCancelledStatusWrites(cancelBody);

        Assert.Equal(1, cancelledWritesInCancel);
        Assert.Equal(cancelledWritesInCancel, cancelledWritesInFile);
    }

    [GeneratedRegex(@"^EmployeeInvoiceStatus\.[A-Za-z0-9_]+$")]
    private static partial Regex LiteralStatus();

    private static int CountCancelledStatusWrites(string text) =>
        StatusWrite().Matches(text)
            .Count(m => m.Groups["rhs"].Value.Trim() == $"EmployeeInvoiceStatus.{nameof(EmployeeInvoiceStatus.Cancelled)}");

    private static void AssertSetterIsNonPublic(string propertyName)
    {
        var setter = typeof(EmployeeInvoice).GetProperty(propertyName)!.GetSetMethod(nonPublic: true);

        Assert.NotNull(setter);
        Assert.False(setter!.IsPublic,
            $"{propertyName} gained a public setter — writers can now live outside EmployeeInvoice.cs " +
            "and this file's scan no longer covers them.");
    }

    private static (string Source, string CancelBody) ReadEntitySource()
    {
        var path = Path.Combine(
            RequireSolutionDirectory(), "Cleansia.Core.Domain", "EmployeePayroll", "EmployeeInvoice.cs");

        Assert.True(File.Exists(path), $"EmployeeInvoice.cs was not found at {path} — the scan resolved nothing.");

        var source = File.ReadAllText(path);
        var cancelBody = ExtractMethodBody(source, "public EmployeeInvoice Cancel(");

        Assert.False(string.IsNullOrWhiteSpace(cancelBody), "The Cancel method body could not be extracted.");

        return (source, cancelBody);
    }

    private static string ExtractMethodBody(string source, string signaturePrefix)
    {
        var signatureAt = source.IndexOf(signaturePrefix, StringComparison.Ordinal);
        Assert.True(signatureAt >= 0, $"'{signaturePrefix}' was not found — the entity was renamed or reshaped.");

        var openingBrace = source.IndexOf('{', signatureAt);
        Assert.True(openingBrace >= 0, "No method body followed the signature.");

        var depth = 0;
        for (var i = openingBrace; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[openingBrace..(i + 1)];
        }

        Assert.Fail("The method body was never closed.");
        return string.Empty;
    }

    private static string RequireSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }

        Assert.Fail("Could not locate the solution directory from the test base directory.");
        return string.Empty;
    }
}
