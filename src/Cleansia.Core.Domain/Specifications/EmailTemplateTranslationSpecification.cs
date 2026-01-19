using System.Linq.Expressions;
using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class EmailTemplateTranslationSpecification : BaseSpecification<string?>, ISpecification<EmailTemplateTranslation>
{
    public string? SearchTerm { get; set; }
    public EmailType? EmailType { get; set; }
    public string? LanguageId { get; set; }

    public Expression<Func<EmailTemplateTranslation, bool>> SatisfiedBy()
    {
        Specification<EmailTemplateTranslation> specification = new TrueSpecification<EmailTemplateTranslation>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<EmailTemplateTranslation>(x => x.Id == Id);
        }

        if (EmailType.HasValue)
        {
            specification &= new DirectSpecification<EmailTemplateTranslation>(x => x.EmailType == EmailType.Value);
        }

        if (!string.IsNullOrWhiteSpace(LanguageId))
        {
            specification &= new DirectSpecification<EmailTemplateTranslation>(x => x.LanguageId == LanguageId);
        }

        if (!string.IsNullOrEmpty(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            specification &= new DirectSpecification<EmailTemplateTranslation>(x =>
                x.Key.ToLower().Contains(searchLower) ||
                x.Value.ToLower().Contains(searchLower)
            );
        }

        return specification.SatisfiedBy();
    }

    public static EmailTemplateTranslationSpecification Create(
        string? id = null,
        string? searchTerm = null,
        EmailType? emailType = null,
        string? languageId = null) =>
        new()
        {
            Id = id,
            SearchTerm = searchTerm,
            EmailType = emailType,
            LanguageId = languageId
        };
}