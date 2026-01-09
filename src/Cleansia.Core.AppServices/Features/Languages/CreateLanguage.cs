using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Languages;

public class CreateLanguage
{
    public record Command(
        string Code,
        string Name) : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ILanguageRepository languageRepository)
        {
            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(5)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .MustAsync(async (code, ct) =>
                    !await languageRepository.ExistsWithCodeAsync(code, ct))
                .WithMessage(BusinessErrorMessage.LanguageCodeAlreadyExists);

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
        public Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var language = Language.Create(command.Code, command.Name);

            languageRepository.Add(language);

            return Task.FromResult(BusinessResult.Success(new Response(language.Id)));
        }
    }
}