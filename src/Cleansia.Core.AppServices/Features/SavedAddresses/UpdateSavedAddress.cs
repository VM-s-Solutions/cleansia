using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.SavedAddresses.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.SavedAddresses;

public class UpdateSavedAddress
{
    public record Command(
        string SavedAddressId,
        string Label,
        string Street,
        string City,
        string ZipCode,
        string? CountryId,
        double Latitude,
        double Longitude
    ) : ICommand<SavedAddressDto>;

    public class Validator : AbstractValidator<Command>
    {
        private readonly ISavedAddressRepository _savedAddressRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            ICountryRepository countryRepository,
            ISavedAddressRepository savedAddressRepository,
            IUserSessionProvider userSessionProvider)
        {
            _savedAddressRepository = savedAddressRepository;
            _userSessionProvider = userSessionProvider;

            RuleFor(x => x.SavedAddressId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotFound)
                .MustAsync(BeOwnedByCallerAsync)
                .WithMessage(BusinessErrorMessage.AddressNotOwnedByUser);

            RuleFor(x => x.Label)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.AddressLabelRequired)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Street)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MinimumLength(5)
                .WithMessage(BusinessErrorMessage.MinLength)
                .MaximumLength(255)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.City)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MinimumLength(2)
                .WithMessage(BusinessErrorMessage.MinLength)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.ZipCode)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MinimumLength(3)
                .WithMessage(BusinessErrorMessage.MinLength)
                .MaximumLength(20)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Latitude)
                .GreaterThanOrEqualTo(GeoBounds.LatMin)
                .WithMessage(BusinessErrorMessage.MapboxCoordsRequired)
                .LessThanOrEqualTo(GeoBounds.LatMax)
                .WithMessage(BusinessErrorMessage.MapboxCoordsRequired);

            RuleFor(x => x.Longitude)
                .GreaterThanOrEqualTo(GeoBounds.LonMin)
                .WithMessage(BusinessErrorMessage.MapboxCoordsRequired)
                .LessThanOrEqualTo(GeoBounds.LonMax)
                .WithMessage(BusinessErrorMessage.MapboxCoordsRequired);

            When(x => !string.IsNullOrEmpty(x.CountryId), () =>
            {
                RuleFor(x => x.CountryId!)
                    .MustAsync(countryRepository.ExistsAsync)
                    .WithMessage(BusinessErrorMessage.NotExistingCountryWithId);
            });
        }

        private async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken)
        {
            return await _savedAddressRepository.GetByIdAsync(id, cancellationToken) != null;
        }

        private async Task<bool> BeOwnedByCallerAsync(string id, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId)) return false;
            var saved = await _savedAddressRepository.GetByIdAsync(id, cancellationToken);
            return saved != null && saved.UserId == userId;
        }
    }

    public class Handler(
        IAddressRepository addressRepository,
        ISavedAddressRepository savedAddressRepository,
        ICountryRepository countryRepository
    ) : ICommandHandler<Command, SavedAddressDto>
    {
        public async Task<BusinessResult<SavedAddressDto>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Existence + ownership enforced by Validator.
            var saved = (await savedAddressRepository.GetByIdAsync(command.SavedAddressId, cancellationToken))!;

            var countryId = command.CountryId;
            if (string.IsNullOrEmpty(countryId))
            {
                var defaultCountry = await countryRepository.GetByIsoCodeAsync(AddressDefaults.FallbackCountryIso, cancellationToken)
                    ?? await countryRepository.GetQueryable().FirstOrDefaultAsync(cancellationToken);
                countryId = defaultCountry?.Id
                    ?? throw new InvalidOperationException("No countries configured");
            }

            var address = await addressRepository.GetAddressAsync(
                command.Street, command.City, command.ZipCode, countryId, cancellationToken);

            if (address == null)
            {
                address = Address.Create(command.Street, command.City, command.ZipCode, countryId, null, command.Latitude, command.Longitude);
                addressRepository.Add(address);
            }

            if (saved.AddressId != address.Id)
            {
                saved.SetAddressId(address.Id);
            }

            saved.UpdateLabel(command.Label);

            var country = await countryRepository.GetByIdAsync(countryId, cancellationToken);
            return BusinessResult.Success(new SavedAddressDto(
                Id: saved.Id,
                Label: saved.Label,
                Street: address.Street,
                City: address.City,
                ZipCode: address.ZipCode,
                State: address.State,
                CountryId: address.CountryId,
                Country: country?.Name,
                Latitude: address.Latitude,
                Longitude: address.Longitude,
                IsDefault: saved.IsDefault));
        }
    }
}
