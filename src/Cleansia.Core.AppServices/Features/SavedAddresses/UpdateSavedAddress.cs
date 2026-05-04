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
        double Longitude,
        string UserId = ""
    ) : ICommand<SavedAddressDto>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICountryRepository countryRepository)
        {
            RuleFor(x => x.SavedAddressId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

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
                .GreaterThanOrEqualTo(-90)
                .LessThanOrEqualTo(90)
                .WithMessage(BusinessErrorMessage.MapboxCoordsRequired);

            RuleFor(x => x.Longitude)
                .GreaterThanOrEqualTo(-180)
                .LessThanOrEqualTo(180)
                .WithMessage(BusinessErrorMessage.MapboxCoordsRequired);

            When(x => !string.IsNullOrEmpty(x.CountryId), () =>
            {
                RuleFor(x => x.CountryId!)
                    .MustAsync(countryRepository.ExistsAsync)
                    .WithMessage(BusinessErrorMessage.NotExistingCountryWithId);
            });
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
            var saved = await savedAddressRepository.GetByIdAsync(command.SavedAddressId, cancellationToken);
            if (saved == null)
            {
                return BusinessResult.Failure<SavedAddressDto>(new Error(
                    nameof(command.SavedAddressId),
                    BusinessErrorMessage.NotFound));
            }

            if (!string.IsNullOrEmpty(command.UserId) && saved.UserId != command.UserId)
            {
                return BusinessResult.Failure<SavedAddressDto>(new Error(
                    nameof(command.UserId),
                    BusinessErrorMessage.AddressNotOwnedByUser));
            }

            var countryId = command.CountryId;
            if (string.IsNullOrEmpty(countryId))
            {
                var defaultCountry = await countryRepository.GetByIsoCodeAsync("CZE", cancellationToken)
                    ?? await countryRepository.GetQueryable().FirstOrDefaultAsync(cancellationToken);
                countryId = defaultCountry?.Id
                    ?? throw new InvalidOperationException("No countries configured");
            }

            // Reuse an existing Address that matches the edited fields, otherwise create a new one.
            // We never mutate an existing Address row — other users/orders may depend on it.
            // Mapbox-supplied coordinates are mandatory at the Validator layer, so we always pass them through.
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
