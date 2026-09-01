using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Services.Geocoding;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Addresses;

/// <summary>
/// Prefix search over the geocoding provider, for the address field a customer is
/// typing into.
///
/// It lives here rather than in the customer app's SSR server because that is the rule
/// for every other outbound integration (ADR-0005 makes the Mapbox client the reference
/// shape) and because the SSR route only existed on one of the three web apps: the
/// partner app has carried a standing manual step asking for exactly this endpoint, and
/// the customer app's own dev server never proxied it, so address search was broken
/// under `nx serve`. Keeping the credential out of the browser — the SSR route's only
/// stated reason — is equally true here, and here it also gets the platform's rate
/// limiter, failure classification and integration metrics.
/// </summary>
public static class SearchAddresses
{
    public record Query(
        string Q,
        string? Country = null,
        string? Language = null,
        int Limit = 5) : IQuery<Response>;

    public record Response(IReadOnlyList<AddressSuggestion> Suggestions);

    public record AddressSuggestion(
        string PlaceName,
        string Street,
        string City,
        string ZipCode,
        double Latitude,
        double Longitude);

    internal class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Q)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(120)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Limit)
                .InclusiveBetween(1, 10)
                .WithMessage(BusinessErrorMessage.MustBePositive);
        }
    }

    internal class Handler(IGeocodingService geocodingService) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var suggestions = await geocodingService.SearchAsync(
                request.Q,
                request.Country,
                request.Language,
                request.Limit,
                cancellationToken);

            // An unprovisioned or degraded provider returns an empty list, not an error:
            // the address field stays usable and the customer types it by hand.
            return BusinessResult.Success(new Response(
                suggestions
                    .Select(s => new AddressSuggestion(
                        s.PlaceName, s.Street, s.City, s.ZipCode, s.Latitude, s.Longitude))
                    .ToList()));
        }
    }
}
