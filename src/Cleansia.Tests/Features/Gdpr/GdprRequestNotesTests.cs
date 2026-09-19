using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// A retried deletion request keeps every attempt on its one row: <c>MarkFailed</c> and
/// <c>MarkCompleted</c> APPEND their note rather than replace it, and when the bounded column overflows
/// the oldest text is what goes — the newest note is the one an admin acts on. A fresh row is unchanged
/// by the appending: the first note simply becomes the note.
/// </summary>
public sealed class GdprRequestNotesTests
{
    [Fact]
    public void The_First_Failure_Note_Becomes_The_Notes_And_Carries_The_Actor()
    {
        var request = GdprRequest.Create("user-1", GdprRequest.DeletionRequestType);

        request.MarkFailed("admin@cleansia.test", "PostgresException: 23503: violates foreign key");

        Assert.Equal(GdprRequestStatus.Failed, request.Status);
        Assert.Equal("admin@cleansia.test", request.ProcessedBy);
        Assert.Equal("PostgresException: 23503: violates foreign key", request.Notes);
        Assert.NotNull(request.CompletedAt);
    }

    [Fact]
    public void A_Second_Failure_Is_Appended_On_Its_Own_Line_And_The_Actor_Is_The_Latest()
    {
        var request = GdprRequest.Create("user-1", GdprRequest.DeletionRequestType);
        request.MarkFailed("customer-1", "first");

        request.MarkFailed("system", "second");

        Assert.Equal("first\nsecond", request.Notes);
        Assert.Equal("system", request.ProcessedBy);
        Assert.Equal(GdprRequestStatus.Failed, request.Status);
    }

    [Fact]
    public void Completing_A_Retried_Request_Keeps_The_Failure_History_Before_Its_Own_Note()
    {
        var request = GdprRequest.Create("user-1", GdprRequest.DeletionRequestType);
        request.MarkFailed("customer-1", "DbUpdateException: boom");

        request.MarkCompleted("admin@cleansia.test", "Retried by admin@cleansia.test");

        Assert.Equal(GdprRequestStatus.Completed, request.Status);
        Assert.Equal("DbUpdateException: boom\nRetried by admin@cleansia.test", request.Notes);
    }

    [Fact]
    public void A_Fresh_Completion_Reads_Exactly_As_Before_Appending_Existed()
    {
        var request = GdprRequest.Create("user-1", GdprRequest.DeletionRequestType);

        request.MarkCompleted("admin@cleansia.test", "Admin deletion by admin@cleansia.test");
        var selfServed = GdprRequest.Create("user-2", GdprRequest.DeletionRequestType).MarkCompleted("user-2", null);

        Assert.Equal("Admin deletion by admin@cleansia.test", request.Notes);
        Assert.Null(selfServed.Notes);
    }

    [Fact]
    public void A_Null_Or_Blank_Note_Leaves_The_Existing_Notes_Alone()
    {
        var request = GdprRequest.Create("user-1", GdprRequest.DeletionRequestType);
        request.MarkFailed("customer-1", "first");

        request.MarkFailed("system", null);
        request.MarkFailed("system", "   ");

        Assert.Equal("first", request.Notes);
    }

    [Fact]
    public void An_Overflowing_History_Drops_Its_Oldest_Text_And_Never_Exceeds_The_Column()
    {
        var request = GdprRequest.Create("user-1", GdprRequest.DeletionRequestType);
        var oldest = new string('a', 600);
        var newest = new string('b', 600);

        request.MarkFailed("system", oldest);
        request.MarkFailed("system", newest);

        Assert.Equal(GdprRequest.NotesMaxLength, request.Notes!.Length);
        Assert.EndsWith(newest, request.Notes);
        Assert.DoesNotContain(oldest, request.Notes);
    }

    [Fact]
    public void A_Single_Note_Longer_Than_The_Column_Keeps_Its_Tail()
    {
        var request = GdprRequest.Create("user-1", GdprRequest.DeletionRequestType);
        var note = new string('x', 1200) + "END";

        request.MarkFailed("system", note);

        Assert.Equal(GdprRequest.NotesMaxLength, request.Notes!.Length);
        Assert.EndsWith("END", request.Notes);
    }
}
