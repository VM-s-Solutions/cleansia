using System.Net;
using Cleansia.Core.Clients.Abstractions;
using FirebaseAdmin.Messaging;
using SendGrid;
using Stripe;

namespace Cleansia.Tests.Integration;

/// <summary>
/// The provider-specific mappers that layer on top of the single
/// <see cref="IntegrationFailureClass"/> taxonomy: a SendGrid <see cref="Response"/>, a
/// <see cref="StripeException"/>, and an FCM <see cref="MessagingErrorCode"/> each resolve to the
/// SAME closed set the raw-status/raw-exception primitives do. There is one taxonomy, not one per
/// provider — these mappers reduce to <see cref="IntegrationFailureClassifier.FromHttpStatus"/> /
/// <see cref="IntegrationFailureClassifier.FromException"/> at the floor.
/// </summary>
public class IntegrationFailureClassifierProviderMappersTests
{
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, IntegrationFailureClass.Transient)]
    [InlineData(HttpStatusCode.ServiceUnavailable, IntegrationFailureClass.Transient)]
    [InlineData(HttpStatusCode.InternalServerError, IntegrationFailureClass.Transient)]
    [InlineData(HttpStatusCode.Unauthorized, IntegrationFailureClass.AuthConfig)]
    [InlineData(HttpStatusCode.Forbidden, IntegrationFailureClass.AuthConfig)]
    [InlineData(HttpStatusCode.BadRequest, IntegrationFailureClass.Permanent)]
    [InlineData(HttpStatusCode.UnprocessableEntity, IntegrationFailureClass.Permanent)]
    public void SendGrid_Response_Classifies_By_Status(HttpStatusCode status, IntegrationFailureClass expected)
    {
        var response = new Response(status, new StringContent(string.Empty), null);

        Assert.Equal(expected, IntegrationFailureClassifier.FromSendGridResponse(response));
    }

    [Fact]
    public void SendGrid_Bad_Template_Or_Invalid_Recipient_400_Is_Permanent_Not_Retryable()
    {
        var response = new Response(
            HttpStatusCode.BadRequest,
            new StringContent("""{"errors":[{"message":"The template_id is invalid"}]}"""),
            null);

        var failure = IntegrationFailureClassifier.FromSendGridResponse(response);

        Assert.Equal(IntegrationFailureClass.Permanent, failure);
        Assert.False(failure.IsRetryable());
    }

    [Theory]
    [InlineData((int)HttpStatusCode.TooManyRequests, IntegrationFailureClass.Transient)]
    [InlineData((int)HttpStatusCode.ServiceUnavailable, IntegrationFailureClass.Transient)]
    [InlineData((int)HttpStatusCode.Unauthorized, IntegrationFailureClass.AuthConfig)]
    [InlineData((int)HttpStatusCode.Forbidden, IntegrationFailureClass.AuthConfig)]
    [InlineData((int)HttpStatusCode.BadRequest, IntegrationFailureClass.Permanent)]
    [InlineData((int)HttpStatusCode.Conflict, IntegrationFailureClass.Permanent)]
    public void Stripe_Exception_With_Http_Status_Classifies_By_Status(int status, IntegrationFailureClass expected)
    {
        var ex = new StripeException { HttpStatusCode = (HttpStatusCode)status };

        Assert.Equal(expected, IntegrationFailureClassifier.FromStripeException(ex));
    }

    [Fact]
    public void Stripe_Exception_Without_Http_Status_Falls_Back_To_Transport_Classification()
    {
        var ex = new StripeException("transport blip") { HttpStatusCode = 0 };

        Assert.Equal(IntegrationFailureClass.Transient, IntegrationFailureClassifier.FromStripeException(ex));
    }

    [Theory]
    [InlineData(MessagingErrorCode.Unavailable, IntegrationFailureClass.Transient)]
    [InlineData(MessagingErrorCode.Internal, IntegrationFailureClass.Transient)]
    [InlineData(MessagingErrorCode.QuotaExceeded, IntegrationFailureClass.Transient)]
    [InlineData(MessagingErrorCode.Unregistered, IntegrationFailureClass.Permanent)]
    [InlineData(MessagingErrorCode.InvalidArgument, IntegrationFailureClass.Permanent)]
    [InlineData(MessagingErrorCode.SenderIdMismatch, IntegrationFailureClass.Permanent)]
    [InlineData(MessagingErrorCode.ThirdPartyAuthError, IntegrationFailureClass.AuthConfig)]
    public void Fcm_Error_Code_Classifies_To_The_Shared_Taxonomy(MessagingErrorCode code, IntegrationFailureClass expected)
    {
        Assert.Equal(expected, IntegrationFailureClassifier.FromFcmErrorCode(code));
    }

    [Fact]
    public void Fcm_Null_Error_Code_Is_Unknown_Transient()
    {
        Assert.Equal(IntegrationFailureClass.Transient, IntegrationFailureClassifier.FromFcmErrorCode(null));
    }

    [Theory]
    [InlineData(MessagingErrorCode.Unregistered, true)]
    [InlineData(MessagingErrorCode.InvalidArgument, true)]
    [InlineData(MessagingErrorCode.SenderIdMismatch, true)]
    [InlineData(MessagingErrorCode.Unavailable, false)]
    [InlineData(MessagingErrorCode.Internal, false)]
    [InlineData(MessagingErrorCode.ThirdPartyAuthError, false)]
    public void Fcm_Dead_Token_Is_Exactly_The_Permanent_Per_Token_Codes(MessagingErrorCode code, bool expectedDead)
    {
        Assert.Equal(expectedDead, IntegrationFailureClassifier.IsDeadFcmToken(code));
    }

    [Fact]
    public void Fcm_Null_Code_Is_Not_A_Dead_Token()
    {
        Assert.False(IntegrationFailureClassifier.IsDeadFcmToken(null));
    }
}
