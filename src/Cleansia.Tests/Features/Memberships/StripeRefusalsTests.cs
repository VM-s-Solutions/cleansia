using Cleansia.Core.Clients.Abstractions.Stripe;
using Stripe;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// The Customer-currency refusal is recognised by Stripe's documented wording, on the error object
/// when there is one and on the exception message otherwise. Stripe gives it no dedicated code, and the
/// sandbox probe of 2026-09-13 did not trigger it at all, so the wording is the only key there is.
/// </summary>
public class StripeRefusalsTests
{
    private const string Wording =
        "You cannot combine currencies on a single customer. This customer has had a subscription, coupon, or invoice item with currency czk";

    [Fact]
    public void TheDocumentedRefusal_OnTheErrorObject_IsRecognised()
    {
        var exception = new StripeException("Request failed")
        {
            StripeError = new StripeError { Type = "invalid_request_error", Message = Wording },
        };

        Assert.True(StripeRefusals.IsCustomerCurrencyLocked(exception));
    }

    [Fact]
    public void TheDocumentedRefusal_WithNoErrorObject_IsRecognisedByTheMessage()
    {
        Assert.True(StripeRefusals.IsCustomerCurrencyLocked(new StripeException(Wording)));
    }

    [Theory]
    [InlineData("Your card was declined.")]
    [InlineData("No such price: 'price_x'")]
    [InlineData("stripe temporarily unavailable")]
    public void AnyOtherRefusal_IsNot(string message)
    {
        var exception = new StripeException(message)
        {
            StripeError = new StripeError { Type = "invalid_request_error", Message = message },
        };

        Assert.False(StripeRefusals.IsCustomerCurrencyLocked(exception));
    }
}
