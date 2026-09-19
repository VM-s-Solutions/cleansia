using Cleansia.Core.AppServices.Features.Gdpr;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// The note a failed erasure leaves names the ROOT cause — an EF <c>DbUpdateException</c> only says to
/// look inside — and never an e-mail address, whatever a message happens to carry: the row is read by
/// admins and kept for years, and the subject's address must not be what it says.
/// </summary>
public sealed class ErasureFailureNoteTests
{
    [Fact]
    public void The_Innermost_Exception_Is_The_One_Described()
    {
        var root = new InvalidOperationException("23503: insert violates foreign key constraint");
        var wrapped = new DbUpdateException("An error occurred while saving the entity changes.", root);

        var note = ErasureFailureNote.Describe(wrapped);

        Assert.Equal("InvalidOperationException: 23503: insert violates foreign key constraint", note);
    }

    [Fact]
    public void An_Exception_Without_An_Inner_One_Is_Described_As_Itself()
    {
        var note = ErasureFailureNote.Describe(new TimeoutException("the walk took too long"));

        Assert.Equal("TimeoutException: the walk took too long", note);
    }

    // The whole whitespace-delimited token goes, punctuation and all: what is blanked is decided by the
    // address in it, not by where the address starts.
    [Fact]
    public void Every_Email_Shaped_Token_In_The_Message_Is_Blanked()
    {
        var exception = new InvalidOperationException(
            "duplicate key (Email)=(jana.novakova@example.com) already exists; also <ops@cleansia.test>");

        var note = ErasureFailureNote.Describe(exception);

        Assert.DoesNotContain("@", note);
        Assert.Equal(
            $"InvalidOperationException: duplicate key {ErasureFailureNote.Redacted} already exists; also {ErasureFailureNote.Redacted}",
            note);
    }
}
