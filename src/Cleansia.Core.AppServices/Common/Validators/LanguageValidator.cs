using Cleansia.Core.Domain.Repositories;
using FluentValidation;

namespace Cleansia.Core.AppServices.Common.Validators;

public class LanguageValidator : AbstractValidator<string>
{
    public LanguageValidator(ILanguageRepository languageRepository)
    {
        RuleFor(lang => lang)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .WithErrorCode("Language")
            .MustAsync(languageRepository.ExistsWithCodeAsync)
            .WithMessage(BusinessErrorMessage.LanguageNotSupported)
            .WithErrorCode("Language");
    }
}