using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Languages;

public class UpdateLanguage
{
    public record Command(
        string LanguageId,
        string Name) : ICommand<Response>;

    public record Response(string Id);

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
                .WithMessage(BusinessErrorMessage.LanguageNotFound);

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength);
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

            language.UpdateName(command.Name);

            return BusinessResult.Success(new Response(language.Id));
        }
    }
}