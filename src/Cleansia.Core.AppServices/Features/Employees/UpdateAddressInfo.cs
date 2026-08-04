#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

public class UpdateAddressInfo
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            ICountryRepository countryRepository,
            IEmployeeRepository employeeRepository,
            IUserSessionProvider userSessionProvider)
        {
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _userSessionProvider = userSessionProvider ?? throw new ArgumentNullException(nameof(userSessionProvider));

            RuleFor(c => c)
                .MustAsync(CallerIsAnEmployee)
                .WithMessage(BusinessErrorMessage.NotAllowedToUpdateEmployee);

            RuleFor(c => c.Street)
                .ValidateStreetAddress();

            RuleFor(c => c.City)
                .ValidateCity();

            RuleFor(c => c.ZipCode)
                .ValidateZipCode();

            RuleFor(c => c.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(countryRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotExistingCountryWithId)
                // Employees can live anywhere within a serviced country —
                // commuters who don't live in a served city are fine. We do
                // block addresses in unsupported countries entirely (no
                // employee record for someone living abroad with no business
                // presence here yet).
                .MustAsync(countryRepository.IsServicedAsync)
                .WithMessage(BusinessErrorMessage.CountryNotServiced);
        }

        // Not an ownership comparison — the subject is server-resolved, so there is nothing for a client
        // to get wrong. What survives is the precondition the handler dereferences.
        private async Task<bool> CallerIsAnEmployee(Command command, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository.GetByUserEmailAsync(
                _userSessionProvider.GetUserEmail() ?? string.Empty, cancellationToken);
            return employee is not null;
        }
    }

    public record Command(
        // [OWN-DATA] (S1): inert. The record written is always the JWT caller's; this stays on the wire
        // only so the shipped clients keep serializing unchanged. Nullable is load-bearing — a
        // non-nullable reference member makes MVC reject an ABSENT id before MediatR is reached.
        string? EmployeeId,
        string Street,
        string City,
        string ZipCode,
        string CountryId,
        string? State = null,
        // Client-supplied coordinates from the map picker. When both are
        // non-null the handler trusts them as-is; when either is null the
        // handler falls back to server-side geocoding via IAddressGeocoder.
        // This keeps web (no map) and partner-mobile (map picker)
        // sharing the same endpoint without lying about precision —
        // map-picked coords reflect the exact pin, not a re-geocoded
        // approximation that might drift 50m to the next building.
        double? Latitude = null,
        double? Longitude = null) : ICommand<Response>;

    public record Response(string EmployeeId);

    internal class Handler(
        IEmployeeRepository employeeRepository,
        IUserSessionProvider userSessionProvider,
        IAddressGeocoder addressGeocoder) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByUserEmailAsync(
                userSessionProvider.GetUserEmail() ?? string.Empty, cancellationToken);

            if (employee is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(BusinessErrorMessage.EmployeeNotFound), BusinessErrorMessage.EmployeeNotFound));
            }

            // Persist text + coords in one Update call. When the client
            // picked a pin on a map (partner mobile) we trust the supplied
            // coords; web's autocomplete also sends coords from Mapbox
            // search results so most callers now provide them. The
            // server-side geocoder only runs as a fallback when both
            // coords are absent (legacy callers, manual text entry).
            var address = employee.Address is not null
                ? employee.Address.Update(
                    command.Street,
                    command.City,
                    command.ZipCode,
                    command.CountryId,
                    command.State,
                    command.Latitude,
                    command.Longitude)
                : Address.Create(
                    command.Street,
                    command.City,
                    command.ZipCode,
                    command.CountryId,
                    command.State,
                    command.Latitude,
                    command.Longitude);

            if (!command.Latitude.HasValue || !command.Longitude.HasValue)
            {
                await addressGeocoder.PopulateCoordinatesAsync(address, cancellationToken);
            }

            employee.UpdateAddress(address);

            return BusinessResult.Success(new Response(employee.Id));
        }
    }
}
