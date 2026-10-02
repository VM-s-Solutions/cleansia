using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

/// <inheritdoc cref="IStripeCustomerResolver"/>
public sealed class StripeCustomerResolver(
    IUserStripeCustomerRepository userStripeCustomerRepository,
    IUserMembershipRepository userMembershipRepository,
    IStripeClient stripeClient,
    ILogger<StripeCustomerResolver> logger) : IStripeCustomerResolver
{
    public async Task<string> ResolveForCurrencyAsync(User user, Currency currency, CancellationToken cancellationToken)
    {
        var existing = await userStripeCustomerRepository.GetForUserInCurrencyAsync(user.Id, currency.Id, cancellationToken);
        if (existing is not null)
        {
            return existing.StripeCustomerId;
        }

        if (await CanAdoptLegacyCustomerAsync(user, currency, cancellationToken))
        {
            AddForAccount(user, currency, user.StripeCustomerId!);
            logger.LogInformation(
                "Adopted the legacy Stripe customer {StripeCustomerId} of user {UserId} for {CurrencyCode}",
                user.StripeCustomerId, user.Id, currency.Code);
            return user.StripeCustomerId!;
        }

        var stripeCustomerId = await stripeClient.CreateCustomerAsync(
            user.Id,
            user.Email,
            $"{user.FirstName} {user.LastName}".Trim(),
            user.PhoneNumber,
            cancellationToken);

        AddForAccount(user, currency, stripeCustomerId);
        if (string.IsNullOrEmpty(user.StripeCustomerId))
        {
            user.AssignStripeCustomerId(stripeCustomerId);
        }

        logger.LogInformation(
            "Created Stripe customer {StripeCustomerId} for user {UserId} in {CurrencyCode}",
            stripeCustomerId, user.Id, currency.Code);
        return stripeCustomerId;
    }

    // The row is the account's, stamped with its company: a booking in another company's market resolves it
    // while committing in that market's company.
    private void AddForAccount(User user, Currency currency, string stripeCustomerId)
    {
        var row = UserStripeCustomer.Create(user.Id, currency.Id, stripeCustomerId);
        row.TenantId = user.TenantId;
        userStripeCustomerRepository.Add(row);
    }

    // The legacy Customer may be adopted for a currency only when Stripe cannot already have locked it
    // to another: it has never billed a membership in another currency, and no per-currency row has
    // claimed it (a checkout abandoned after adoption leaves a row with no membership behind it).
    private async Task<bool> CanAdoptLegacyCustomerAsync(User user, Currency currency, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(user.StripeCustomerId))
        {
            return false;
        }

        if (await userStripeCustomerRepository.IsStripeCustomerIdClaimedAsync(user.StripeCustomerId, cancellationToken))
        {
            return false;
        }

        return !await userMembershipRepository.HasAnyInOtherCurrencyAsync(user.Id, currency.Id, cancellationToken);
    }
}
