using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Languages;

public class DeleteLanguage
{
    public record Command(string LanguageId) : ICommand<Response>;

    public record Response(bool Success);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ILanguageRepository languageRepository)
        {
            RuleFor(x => x.LanguageId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) =>
                    await languageRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.LanguageNotFound)
                .MustAsync(async (id, ct) =>
                    !await languageRepository.IsInUseAsync(id, ct))
                .WithMessage(BusinessErrorMessage.LanguageInUse);
        }
    }

    internal class Handler(ILanguageRepository languageRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var language = await languageRepository.GetByIdAsync(command.LanguageId, cancellationToken);

            if (language is null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.LanguageId), BusinessErrorMessage.LanguageNotFound));
            }

            // Double-check in handler as well for safety
            var isInUse = await languageRepository.IsInUseAsync(command.LanguageId, cancellationToken);
            if (isInUse)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.LanguageId), BusinessErrorMessage.LanguageInUse));
            }

            languageRepository.Remove(language);

            return BusinessResult.Success(new Response(true));
        }
    }
}