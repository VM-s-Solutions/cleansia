#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Users;

/// <summary>
/// Sets or clears the signed-in customer's avatar, and touches NOTHING else.
///
/// <para><b>Why this exists as its own command.</b> Changing the photo used to go out as a full
/// <see cref="UpdateCurrentUser.Command"/> carrying first name, last name, phone and birth date,
/// because that was the only endpoint that could move an avatar. So a picture upload was validated
/// as if it were a profile save: an account with no phone number was rejected on the phone rule,
/// and the customer was told something was wrong with a field they had not touched. Owner,
/// 2026-09-03: "I'd like to make a photo manipulations separately from the other profile details."
///
/// <para>The two are genuinely different edits. This one has exactly one precondition — the image
/// is an image — so it carries exactly one rule, and no future rule about names or numbers can ever
/// block it again.</para>
///
/// <para>The blob work is <see cref="ProfilePhotoUpdater"/>, shared verbatim with the profile save
/// so the two cannot drift on upload order, on the fresh-name-per-upload rule, or on deleting the
/// superseded blob only after the new one is safely up.</para>
/// </summary>
public class UpdateCurrentUserPhoto
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            // The only thing that can be wrong with this request.
            RuleFor(c => c.Photo)
                .SetValidator(new ImageFileValidator()!)
                .When(command => !string.IsNullOrWhiteSpace(command.Photo?.Base64Content));

            // A request that neither carries an image nor asks for removal says nothing at all.
            RuleFor(c => c)
                .Must(c => !string.IsNullOrWhiteSpace(c.Photo?.Base64Content) || c.RemovePhoto)
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Photo));
        }
    }

    /// <param name="Photo">The new avatar, or null when <paramref name="RemovePhoto"/> is set.</param>
    /// <param name="RemovePhoto">Clears the avatar. Ignored when an image is supplied.</param>
    public record Command(BlobFileDto? Photo, bool RemovePhoto) : ICommand<Response>;

    public record Response(string Id);

    public class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IBlobContainerClientFactory clientFactory) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByIdAsync(userSessionProvider.GetUserId()!, cancellationToken);
            if (user is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    "Authentication", BusinessErrorMessage.NotExistingUserWithId));
            }

            await ProfilePhotoUpdater.ApplyAsync(
                user, command.Photo, command.RemovePhoto, clientFactory, cancellationToken);

            return BusinessResult.Success(new Response(Id: user.Id));
        }
    }
}
