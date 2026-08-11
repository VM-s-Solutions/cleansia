using System.Reflection;
using AppConstants = Cleansia.Core.AppServices.Common.Constants;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// Every blob container the platform names, with a written verdict on whether a GDPR erasure deletes
/// from it.
///
/// <para>Erasure deleted from three containers out of seven for as long as there were more than three,
/// and dispute evidence was missed not because anyone decided to keep it but because no artifact
/// anywhere stated how many containers exist. <c>docs/architecture/infrastructure.md</c> lists five;
/// <c>storage.bicep</c> provisions six; <c>Constants.BlobContainers</c> declares seven. Three lists, no
/// two the same, and none of them says which ones an erasure has to reach.</para>
///
/// <para>So the roster is the statement, and it is checked against the erasure service's own source
/// rather than against a copy of it: a container marked <see cref="Verdict.ErasedOnRequest"/> must be
/// named in <c>GdprDeletionService</c>, and one marked otherwise must not be. Adding a container
/// without a verdict, or dropping a container's deletion, reddens this.</para>
/// </summary>
public class BlobContainerErasureRosterTests
{
    public enum Verdict
    {
        /// <summary>Holds the data subject's own content — the erasure deletes it.</summary>
        ErasedOnRequest,

        /// <summary>
        /// A rendered financial document. ADR-0007 D4 keeps the underlying rows (receipts, invoices)
        /// rather than removing them, so the erasure deliberately leaves the PDF that is that record.
        /// Whether the subject's name and address inside those PDFs must be redacted at rest is an open
        /// owner/legal question, NOT something this roster decides — it records only that no code
        /// deletes them today, and why.
        /// </summary>
        RetainedFinancialRecord,

        /// <summary>Declared but has no reader, no writer and no provisioned container.</summary>
        Unused
    }

    private static readonly Dictionary<string, Verdict> Roster = new()
    {
        [AppConstants.BlobContainers.UserFiles] = Verdict.ErasedOnRequest,
        [AppConstants.BlobContainers.EmployeeDocuments] = Verdict.ErasedOnRequest,
        [AppConstants.BlobContainers.OrderPhotos] = Verdict.ErasedOnRequest,
        [AppConstants.BlobContainers.DisputeEvidence] = Verdict.ErasedOnRequest,
        [AppConstants.BlobContainers.GeneratedInvoices] = Verdict.RetainedFinancialRecord,
        [AppConstants.BlobContainers.GeneratedReceipts] = Verdict.RetainedFinancialRecord,
        [AppConstants.BlobContainers.BetaWhiteList] = Verdict.Unused
    };

    [Fact]
    public void Every_Declared_Container_Carries_An_Erasure_Verdict()
    {
        var declared = typeof(AppConstants.BlobContainers)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order()
            .ToList();

        Assert.Equal(Roster.Count, declared.Count);
        Assert.Equal(Roster.Keys.Order().ToList(), declared);
    }

    [Theory]
    [InlineData(Verdict.ErasedOnRequest, true)]
    [InlineData(Verdict.RetainedFinancialRecord, false)]
    [InlineData(Verdict.Unused, false)]
    public void The_Erasure_Service_Names_Exactly_The_Containers_Its_Verdict_Requires(
        Verdict verdict, bool expectedInDeletionService)
    {
        var source = File.ReadAllText(Path.Combine(
            SourceRoot().FullName,
            "Cleansia.Core.AppServices", "Services", "GdprDeletionService.cs"));

        foreach (var container in Roster.Where(entry => entry.Value == verdict).Select(entry => entry.Key))
        {
            var symbol = $"Constants.BlobContainers.{NameOf(container)}";
            Assert.Equal(expectedInDeletionService, source.Contains(symbol, StringComparison.Ordinal));
        }
    }

    private static string NameOf(string containerValue) =>
        typeof(AppConstants.BlobContainers)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Single(field => field.IsLiteral && (string?)field.GetRawConstantValue() == containerValue)
            .Name;

    private static DirectoryInfo SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.EnumerateFiles("Cleansia.Api.sln").Any())
        {
            dir = dir.Parent;
        }

        Assert.True(
            dir is not null,
            $"Could not find Cleansia.Api.sln walking up from {AppContext.BaseDirectory}. "
                + "If the solution moved, update this test.");

        return dir!;
    }
}
