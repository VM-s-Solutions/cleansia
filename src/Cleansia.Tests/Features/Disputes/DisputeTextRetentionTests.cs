using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// Owner ruling 2026-09-14: an erasure keeps the dispute's text readable for three years, because
/// a chargeback or a court claim on that order may still turn on what the customer wrote and what was
/// answered — and the evidence FILES go at erasure regardless (their names are the subject's own). So
/// the aggregate has two steps where it had one: the erasure blanks the evidence rows and stamps the
/// window; the retention sweep blanks everything and clears the stamp once the window is past.
/// </summary>
public class DisputeTextRetentionTests
{
    private const string Description = "The bathroom was left dirty and the sink was not cleaned.";
    private const string CustomerMessage = "Photos attached, the tiles are still grey.";
    private const string StaffMessage = "We are sorry — a partial refund is on its way.";
    private static readonly DateTimeOffset RetainedUntil = new(2029, 9, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_New_Dispute_Carries_No_Retention_Stamp()
    {
        var dispute = NewDispute();

        Assert.Null(dispute.TextRetainedUntil);
    }

    [Fact]
    public void RetainTextUntil_Stamps_The_Window_And_Leaves_Every_Text_Readable()
    {
        var dispute = NewDispute();
        dispute.Resolve("admin-1", 250m, "Half the price back, the tiles were verified grey.");

        dispute.RetainTextUntil(RetainedUntil);

        Assert.Equal(RetainedUntil, dispute.TextRetainedUntil);
        Assert.Equal(Description, dispute.Description);
        Assert.Equal("Half the price back, the tiles were verified grey.", dispute.ResolutionNotes);
        Assert.Equal([CustomerMessage, StaffMessage], dispute.Messages.Select(m => m.Message));
    }

    [Fact]
    public void AnonymizeEvidence_Blanks_The_File_Rows_And_Nothing_Else()
    {
        var dispute = NewDispute();

        dispute.AnonymizeEvidence();

        var evidence = Assert.Single(dispute.Evidence);
        Assert.Equal(AnonymizationMarker.Value, evidence.FileName);
        Assert.Equal(AnonymizationMarker.Value, evidence.FilePath);
        Assert.Equal(Description, dispute.Description);
        Assert.Equal([CustomerMessage, StaffMessage], dispute.Messages.Select(m => m.Message));
        Assert.Null(dispute.TextRetainedUntil);
    }

    [Fact]
    public void Anonymize_Blanks_Every_Text_And_Clears_The_Stamp()
    {
        var dispute = NewDispute();
        dispute.Resolve("admin-1", 250m, "Half the price back.");
        dispute.RetainTextUntil(RetainedUntil);

        dispute.Anonymize();

        Assert.Equal(AnonymizationMarker.Value, dispute.Description);
        Assert.Equal(AnonymizationMarker.Value, dispute.ResolutionNotes);
        Assert.All(dispute.Messages, m => Assert.Equal(AnonymizationMarker.Value, m.Message));
        Assert.All(dispute.Evidence, e =>
        {
            Assert.Equal(AnonymizationMarker.Value, e.FileName);
            Assert.Equal(AnonymizationMarker.Value, e.FilePath);
        });
        Assert.Null(dispute.TextRetainedUntil);
    }

    [Fact]
    public void Anonymize_Leaves_Absent_Resolution_Notes_Absent()
    {
        var dispute = NewDispute();

        dispute.Anonymize();

        Assert.Null(dispute.ResolutionNotes);
    }

    private static Dispute NewDispute()
    {
        var dispute = new Dispute("order-1", "user-1", DisputeReason.QualityIssue, Description, "user-1");
        dispute.AddMessage(CustomerMessage, "user-1", isStaff: false);
        dispute.AddMessage(StaffMessage, "admin-1", isStaff: true);
        dispute.AddEvidence("bathroom-sink.jpg", "dispute-1/2f9c1a4b7d6e4f0b9c3a5e8d1f2b4c60.jpg", "user-1");
        return dispute;
    }
}
