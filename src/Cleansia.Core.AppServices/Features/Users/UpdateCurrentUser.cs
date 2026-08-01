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

        // No longer an ownership comparison — the subject is server-resolved, so there is nothing for a
        // client to get wrong. What survives is the precondition the handler dereferences: the JWT
        // subject must resolve to a row.
        private async Task<bool> AllowedToUpdateUser(Command command, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(
                _userSessionProvider.GetUserId() ?? string.Empty, cancellationToken);
            return user is not null;
        }

        private async Task<bool> UserWithPhoneNumberNotExistsAsync(Command command, string phoneNumber,
            CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
            return user?.Id is null || user.Id == _userSessionProvider.GetUserId();
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
        // [OWN-DATA] (S1): the row written is ALWAYS the JWT caller's — this id is never read. It stays
        // on the wire only so the Android/iOS clients, which decode their own token to fill it, keep
        // deserializing unchanged. Nullable because the web clients CANNOT fill it (the session is an
        // HttpOnly cookie and MyProfileDto carries no id) and a non-nullable reference parameter makes
        // MVC reject the absent member with "The Id field is required." before this command is ever
        // dispatched.
        string? Id,
        string FirstName,
        string LastName,
        string PhoneNumber,
        DateOnly? BirthDate,
        BlobFileDto? Photo,
        string? LanguageCode,
        // Removal has to be asked for. Photo carries a NEW image only; a null/blank Photo means the
        // client simply had nothing to say about the avatar, which is what every profile save from
        // Android, iOS and web looks like. Defaulted so those callers keep compiling and stay safe.
        bool RemovePhoto = false) : ICommand<Response>;

    public record Response(string Id);

    public class Handler(
        IUserRepository userRepository,
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider,
        IBlobContainerClientFactory clientFactory) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByIdAsync(userSessionProvider.GetUserId()!, cancellationToken);
            var userOrders = await orderRepository.GetOrdersByPhoneNumberAsync(
                user!.PhoneNumber ?? string.Empty, cancellationToken);

            await UpdateProfilePhoto(user, command, cancellationToken);
            UpdateUserAndOrders(user, userOrders, command);

            return BusinessResult.Success(new Response(Id: user.Id));
        }

        private async Task UpdateProfilePhoto(User user, Command command, CancellationToken cancellationToken)
        {
            var hasNewPhoto = !string.IsNullOrWhiteSpace(command.Photo?.Base64Content);

            // A save that neither carries an image nor asks for removal says nothing about the avatar,
            // so it must not touch it. This is the shape of every profile save the clients send.
            if (!hasNewPhoto && !command.RemovePhoto)
            {
                return;
            }

            var supersededPhotoName = user.ProfilePhotoName;
            var client = clientFactory.GetBlobContainerClient(Constants.BlobContainers.UserFiles);

            if (hasNewPhoto)
            {
                // A fresh name per upload keeps the stored name content-addressed: clients cache their
                // avatar bitmap on it, so reusing it would render the previous image forever, and an
                // outstanding SAS keeps resolving to the image it was issued for. Upload BEFORE
                // deleting — a failed upload must not destroy the avatar the user still has.
                var fileName = Guid.NewGuid().ToString();
                await UploadPhotoAsync(client, fileName, command.Photo!.Base64Content!, cancellationToken);
                user.UpdateProfilePhotoName(fileName);
            }
            else
            {
                user.UpdateProfilePhotoName(null);
            }

            if (!string.IsNullOrWhiteSpace(supersededPhotoName))
            {
                await client.DeleteAsync(supersededPhotoName, cancellationToken);
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