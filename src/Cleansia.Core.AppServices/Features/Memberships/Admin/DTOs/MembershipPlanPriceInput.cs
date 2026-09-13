namespace Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;

/// <summary>One currency's entry on the admin plan form: the price of one billing period and the Stripe Price that charges it.</summary>
public record MembershipPlanPriceInput(decimal Price, string StripePriceId);
