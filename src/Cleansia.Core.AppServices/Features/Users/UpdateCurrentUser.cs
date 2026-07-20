#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Users;

public class UpdateCurrentUser
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _userSessionProvider = userSessionProvider ?? throw new ArgumentNullException(nameof(userSessionProvider));

            RuleFor(c => c).SetValidator(new UserEmailValidator<Command>(userRepository, userSessionProvider));

            RuleFor(c => c)
                .MustAsync(AllowedToUpdateUser)
                .WithMessage(BusinessErrorMessage.NotAllowedToUpdateUser)
                .WithErrorCode(nameof(User.Email));

            RuleFor(c => c.FirstName).ValidateFirstName();

            RuleFor(c => c.LastName).ValidateLastName();

            RuleFor(c => c.BirthDate)
                .Cascade(CascadeMode.Stop)
                .Must(BeAValidDate)
                .WithMessage(BusinessErrorMessage.InvalidDate)
                .WithErrorCode(nameof(Command.BirthDate))
                .Must(BeInPast)
                .WithMessage(BusinessErrorMessage.DateMustBeInPast)
                .WithErrorCode(nameof(Command.BirthDate))
                .Must(BeReasonableAge)
                .WithMessage(BusinessErrorMessage.InvalidAge)
                .WithErrorCode(nameof(Command.BirthDate))
                .When(c => c.BirthDate.HasValue);

            RuleFor(c => c.PhoneNumber)
                .MustAsync(UserWithPhoneNumberNotExistsAsync)
                .WithMessage(BusinessErrorMessage.ExistingPhoneNumber)
                .WithErrorCode(nameof(Command.PhoneNumber));

            RuleFor(c => c.Photo)
                .SetValidator(new ImageFileValidator()!)
                .When(command => !string.IsNullOrWhiteSpace(command.Photo?.Base64Content));
        }

        private async Task<bool> AllowedToUpdateUser(Command command, CancellationToken cancellationToken)
        {
            var currentUserEmail = _userSessionProvider.GetUserEmail();
            var user = await _userRepository.GetByEmailAsync(currentUserEmail ?? string.Empty, cancellationToken);
            return user?.Id == command.Id;
        }

        private async Task<bool> UserWithPhoneNumberNotExistsAsync(Command command, string phoneNumber,
            CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
            return user?.Id is null || user.Id == command.Id;
        }

        private static bool BeAValidDate(DateOnly? date)
        {
            return !date.HasValue || date.Value != default;
        }

        private static bool BeInPast(DateOnly? date)
        {
            return date < DateOnly.FromDateTime(DateTime.Today);
        }

        private static bool BeReasonableAge(DateOnly? date)
        {
            var minDate = DateOnly.FromDateTime(DateTime.Today).AddYears(-120);
            return date >= minDate && date <= DateOnly.FromDateTime(DateTime.Today);
        }
    }

    public record Command(
        string Id,
        string FirstName,
        string LastName,
        string PhoneNumber,
        DateOnly? BirthDate,
        BlobFileDto? Photo,
        string? LanguageCode) : ICommand<Response>;

    public record Response(string Id);

    internal class Handler(
        IUserRepository userRepository,
        IOrderRepository orderRepository,
        IBlobContainerClientFactory clientFactory) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByIdAsync(command.Id, cancellationToken);
            var userOrders = await orderRepository.GetOrdersByPhoneNumberAsync(
                user!.PhoneNumber ?? string.Empty, cancellationToken);

            await UpdateProfilePhoto(user, command, cancellationToken);
            UpdateUserAndOrders(user, userOrders, command);

            return BusinessResult.Success(new Response(Id: user.Id));
        }

        private async Task UpdateProfilePhoto(User user, Command command, CancellationToken cancellationToken)
        {
            var hasExistingPhoto = !string.IsNullOrWhiteSpace(user.ProfilePhotoName);
            var hasNewPhoto = !string.IsNullOrWhiteSpace(command.Photo?.Base64Content);

            var client = clientFactory.GetBlobContainerClient(Constants.BlobContainers.UserFiles);

            switch ((hasExistingPhoto, hasNewPhoto))
            {
                case (true, true):
                    await client.DeleteAsync(user.ProfilePhotoName!, cancellationToken);
                    await UploadPhotoAsync(client, user.ProfilePhotoName!, command.Photo!.Base64Content!,
                        cancellationToken);
                    break;
                case (false, true):
                    {
                        var fileName = Guid.NewGuid().ToString();
                        await UploadPhotoAsync(client, fileName, command.Photo!.Base64Content!, cancellationToken);
                        user.UpdateProfilePhotoName(fileName);
                        break;
                    }
                case (true, false) when
                    string.IsNullOrWhiteSpace(command.Photo?.FileName):
                    await client.DeleteAsync(user.ProfilePhotoName!, cancellationToken);
                    user.UpdateProfilePhotoName(null);
                    break;
            }
        }

        private static async Task UploadPhotoAsync(IBlobContainerClient client, string fileName, string base64Content, CancellationToken cancellationToken)
        {
            await using var stream = new MemoryStream(Convert.FromBase64String(base64Content.ExtractBase64Data()));
            await client.UploadAsync(fileName, stream, Metadata.CacheMetadata, cancellationToken);
        }

        private static void UpdateUserAndOrders(User user, IReadOnlyList<Order> userOrders, Command command)
        {
            foreach (var order in userOrders)
            {
                order.UpdatePhone(command.PhoneNumber);
            }

            user.Update(command.FirstName, command.LastName, command.PhoneNumber, command.BirthDate);

            if (!string.IsNullOrWhiteSpace(command.LanguageCode))
            {
                user.UpdateLanguagePreference(command.LanguageCode);
            }
        }
    }
}