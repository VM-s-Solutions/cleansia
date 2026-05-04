using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.SavedAddresses;

public class DeleteSavedAddress
{
    public record Command(string SavedAddressId, string UserId = "") : ICommand;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.SavedAddressId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(ISavedAddressRepository savedAddressRepository) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
        {
            var saved = await savedAddressRepository.GetByIdAsync(command.SavedAddressId, cancellationToken);
            if (saved == null)
            {
                return BusinessResult.Failure(new Error(
                    nameof(command.SavedAddressId),
                    BusinessErrorMessage.NotFound));
            }

            if (!string.IsNullOrEmpty(command.UserId) && saved.UserId != command.UserId)
            {
                return BusinessResult.Failure(new Error(
                    nameof(command.UserId),
                    BusinessErrorMessage.AddressNotOwnedByUser));
            }

            savedAddressRepository.Remove(saved);
            return BusinessResult.Success();
        }
    }
}
