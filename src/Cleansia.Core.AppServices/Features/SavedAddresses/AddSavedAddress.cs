using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.SavedAddresses.DTOs;
using Cleansia.Core.AppServices.Features.SavedAddresses.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.SavedAddresses;

public class AddSavedAddress
{
    public record Command(
        string Label,
        string Street,
        string City,
        string ZipCode,
        string? CountryId,
        bool SetAsDefault,
        double Latitude,
        double Longitude
    ) : ICommand<SavedAddressDto>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICountryRepository countryRepository)
        {
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
    }

    public class Handler(
        IAddressRepository addressRepository,
        ISavedAddressRepository savedAddressRepository,
        ICountryRepository countryRepository,
        IUserSessionProvider userSessionProvider
    ) : ICommandHandler<Command, SavedAddressDto>
    {
        public async Task<BusinessResult<SavedAddressDto>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var countryId = await ResolveCountryIdAsync(command.CountryId, cancellationToken);
            var address = await GetOrCreateAddressAsync(command, countryId, cancellationToken);

            if (!string.IsNullOrEmpty(address.Id) && await IsAlreadySavedAsync(userId, address.Id, cancellationToken))
            {
                return BusinessResult.Failure<SavedAddressDto>(new Error(
                    nameof(command.Street),
                    BusinessErrorMessage.SavedAddressAlreadyExists));
            }

            if (address.Id == null || await addressRepository.GetByIdAsync(address.Id, cancellationToken) == null)
            {
                addressRepository.Add(address);
            }

            if (command.SetAsDefault)
            {
                await savedAddressRepository.ClearDefaultForUserAsync(userId, cancellationToken);
            }

            var saved = SavedAddress.Create(
                userId: userId,
                addressId: address.Id,
                label: command.Label,
                isDefault: command.SetAsDefault);

            savedAddressRepository.Add(saved);

            var country = await countryRepository.GetByIdAsync(countryId, cancellationToken);
            return BusinessResult.Success(saved.MapToDto(address, country?.Name));
        }

        private async Task<string> ResolveCountryIdAsync(string? requestedCountryId, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(requestedCountryId))
            {
                return requestedCountryId;
            }

            var defaultCountry = await countryRepository.GetByIsoCodeAsync(AddressDefaults.FallbackCountryIso, cancellationToken)
                ?? await countryRepository.GetQueryable().FirstOrDefaultAsync(cancellationToken);
            return defaultCountry?.Id
                ?? throw new InvalidOperationException("No countries configured");
        }

        private async Task<Address> GetOrCreateAddressAsync(Command command, string countryId, CancellationToken cancellationToken) =>
            await addressRepository.GetAddressAsync(
                command.Street, command.City, command.ZipCode, countryId, cancellationToken)
            ?? Address.Create(command.Street, command.City, command.ZipCode, countryId, null, command.Latitude, command.Longitude);

        private async Task<bool> IsAlreadySavedAsync(string userId, string addressId, CancellationToken cancellationToken)
        {
            var existing = await savedAddressRepository.GetByUserAsync(userId, cancellationToken);
            return existing.Any(s => s.AddressId == addressId);
        }
    }
}
