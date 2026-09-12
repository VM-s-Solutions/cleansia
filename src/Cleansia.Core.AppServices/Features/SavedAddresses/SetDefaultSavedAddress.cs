using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.SavedAddresses;

public class SetDefaultSavedAddress
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
            var saved = (await savedAddressRepository.GetByIdAsync(command.SavedAddressId, cancellationToken))!;

            // THIS COULD NOT SUCCEED. `IX_SavedAddresses_UserId_Default_Unique` is a partial unique
            // index, which Postgres cannot defer, so it is checked at the end of every statement and the
            // instant two of a user's addresses are default is a violation rather than an intermediate
            // state. EF emits the UPDATEs in the order the entities entered the CHANGE TRACKER, not the
            // order they were mutated -- and the promote target is loaded on the line above, before
            // ClearDefaultForUserAsync loads the current default. So the promote was always emitted
            // first, and every attempt to change a default address raised an unhandled 23505: a 500 on
            // the customer web and mobile hosts, on an action a customer takes to fix their own data.
            //
            // Found by the adversarial review of the currency change that has the identical shape, not
            // by the suite -- this handler had no test of any kind, in any project.
            //
            // The clear is therefore flushed before the promote is emitted, and both sit in one
            // transaction so no observer sees the user with no default at all.
            await using var transaction = await savedAddressRepository.BeginTransactionAsync(cancellationToken);

            await savedAddressRepository.ClearDefaultForUserAsync(saved.UserId, cancellationToken);
            await savedAddressRepository.CommitAsync(cancellationToken);

            saved.SetDefault(true);
            await savedAddressRepository.CommitAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return BusinessResult.Success();
        }
    }
}
