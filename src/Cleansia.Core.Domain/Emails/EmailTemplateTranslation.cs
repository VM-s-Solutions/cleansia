using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Emails;

public class EmailTemplateTranslation : Auditable
{
    [Required]
    [MaxLength(100)]
    public string Key { get; private set; } = default!;

    [Required]
    [MaxLength(5000)]
    public string Value { get; private set; } = default!;

    public EmailType EmailType { get; private set; }

    public string LanguageId { get; private set; } = default!;
    public Language? Language { get; private set; }

    public static EmailTemplateTranslation Create(
        string key,
        string value,
        EmailType emailType,
        string languageId)
    {
        return new EmailTemplateTranslation
        {
            Key = key,
            Value = value,
            EmailType = emailType,
            LanguageId = languageId
        };
    }

    public EmailTemplateTranslation UpdateValue(string newValue)
    {
        Value = newValue;
        return this;
    }
}
