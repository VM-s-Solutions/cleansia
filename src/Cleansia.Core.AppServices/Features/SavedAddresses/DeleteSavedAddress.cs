using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.SavedAddresses;

public class DeleteSavedAddress
{
    public record Command(string SavedAddressId) : ICommand;

    public class Validator : AbstractValidator<Command>
    {
        private readonly ISavedAddressRepository _savedAddressRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
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
        ISavedAddressRepository savedAddressRepository) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
        {
            // Existence + ownership are enforced in the validator.
            var saved = (await savedAddressRepository.GetByIdAsync(command.SavedAddressId, cancellationToken))!;
            savedAddressRepository.Remove(saved);
            return BusinessResult.Success();
        }
    }
}
