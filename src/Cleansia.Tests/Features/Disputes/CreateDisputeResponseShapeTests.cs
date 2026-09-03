using Cleansia.Config.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// What <c>Dispute/Create</c> actually puts on the wire.
///
/// <para>Owner, 2026-09-03: a photo attached while FILING a dispute never arrived. The dispute was
/// created, the evidence endpoint worked, and nothing errored — the caller simply had no id to
/// upload the evidence against, because this endpoint answered with an EMPTY 200 body.</para>
///
/// <para>The cause was one type argument. <c>HandleSuccess&lt;T&gt;</c> matches
/// <c>BusinessResult&lt;T&gt;</c> BY T, and the controller passed <c>string</c> against a
/// <c>BusinessResult&lt;CreateDispute.Response&gt;</c>. No match, so it fell through to the
/// <c>_ =&gt; Ok()</c> arm — an empty body, whatever <c>ProducesResponseType</c> advertised. A
/// mismatch like that is invisible: it compiles, it returns 200, and the only symptom is a caller
/// quietly receiving nothing.</para>
///
/// <para>These tests pin the BODY rather than the status code, because the status code was never
/// the thing that was wrong.</para>
/// </summary>
public class CreateDisputeResponseShapeTests
{
    private sealed class TestController : CleansiaApiController
    {
        public IActionResult Handle<T>(BusinessResult result) => HandleResult<T>(result);
    }

    /// <summary>
    /// The regression, at the level it actually broke: the id reaches the caller.
    /// </summary>
    [Fact]
    public void A_Created_Dispute_Answers_With_Its_Id()
    {
        var result = BusinessResult.Success(new CreateDispute.Response("dispute-1"));

        var action = new TestController().Handle<CreateDispute.Response>(result);

        var ok = Assert.IsType<OkObjectResult>(action);
        var body = Assert.IsType<CreateDispute.Response>(ok.Value);
        Assert.Equal("dispute-1", body.DisputeId);
    }

    /// <summary>
    /// The bug itself, pinned so it cannot come back by another route: ask for the wrong T and the
    /// body silently disappears. Nothing throws, nothing warns, the status is still 200.
    /// </summary>
    [Fact]
    public void Asking_For_The_Wrong_Type_Silently_Empties_The_Body()
    {
        var result = BusinessResult.Success(new CreateDispute.Response("dispute-1"));

        var action = new TestController().Handle<string>(result);

        // No value at all — this is what the customer client received for months.
        Assert.IsType<OkResult>(action);
    }
}
