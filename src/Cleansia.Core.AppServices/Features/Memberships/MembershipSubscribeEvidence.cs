using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.AppServices.Features.Memberships;

/// <summary>
/// What a <c>customer.membership.subscribe</c> row records: the plan and the price the customer was
/// about to pay, in the market's currency, and the trial they were granted (ADR-0062 D3). Top-level
/// rather than nested because both subscribe surfaces emit it. <see cref="Reconciled"/> marks a
/// replayed confirm that resolved to the membership an earlier attempt already created — the second row
/// is the customer's second submit, not a second purchase.
/// </summary>
public record MembershipSubscribeEvidence(
    string PlanCode,
    string CurrencyCode,
    decimal Price,
    decimal MonthlyEquivalentPrice,
    string? CountryId,
    int? TrialDays,
    MembershipSubscribeChannel Channel,
    bool Reconciled) : ICustomerAuditPayload
{
    public static MembershipSubscribeEvidence For(
        MembershipPlan plan,
        MembershipPlanPrice price,
        string currencyCode,
        string? countryId,
        int trialDays,
        MembershipSubscribeChannel channel,
        bool reconciled = false) => new(
        PlanCode: plan.Code,
        CurrencyCode: currencyCode,
        Price: price.Price,
        MonthlyEquivalentPrice: plan.MonthlyEquivalentOf(price.Price),
        CountryId: countryId,
        TrialDays: trialDays > 0 ? trialDays : null,
        Channel: channel,
        Reconciled: reconciled);
}
